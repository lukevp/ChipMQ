using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emulator
{

    public class CPU : ICPU
    {

        // 4K of ram
        private byte[] ram = new byte[0x1000];

        // 16 registers
        private byte[] registers = new byte[0x10];

        // 16-bit address pointer
        private int i = 0;

        // 16-bit program counter
        private int pc = 512;

        // start with 16 levels of stack.
        private Stack<int> s = new Stack<int>(16);

        private byte delayTimer = 0;
        private byte soundTimer = 0;

        
        private bool[,] display = new bool[32, 64];


        // 16 keys that can be pressed at any one time.
        private bool[] keys = new bool[16];

        private double millisecondsPerCPUStep;
        private double millisecondsPerTimerStep = (1000.0 / 60);

        private Stopwatch stopwatch;
        

        private double lastMilliseconds = 0;
        private double accumulatedMillisecondsCPU = 0;
        private double accumulatedMillisecondsTimer = 0;

        // watch display and generate a new compressed display with preamble bit for filtering by clients
        // but only if the display is marked stale.
        private bool isDisplayStale = true;
        private byte[] compressedDisplay = new byte[(64 * 4) + 1];
        public byte[] DisplayArray
        {
            get
            {
                if (isDisplayStale)
                {
                    // 64 x 32 pixel display - but it's only black / white so we are bit packing the X axis into 32/8 bytes = 4 bytes per, * 64 columns.
                    isDisplayStale = false;
                    // TODO: create a shared library that can serialize/deserialize these objects and just returns the types we need.

                    compressedDisplay[0] = 0x01; // 0x01 is a display communication.
                    int counter = 1;
                    for (int y = 0; y < 32; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            byte b = 0;
                            for (int subx = 0; subx < 8; subx++)
                            {
                                if (display[y, (x*8)+subx])
                                {
                                    b |= (byte)(1 << (7 - subx));
                                }
                            }
                            compressedDisplay[counter] = b;
                            counter += 1;
                        }
                    }
                }
                return compressedDisplay;
            }
        }

        private bool keyPressedSinceReset = false;

        public void PressKey(byte key)
        {
            keys[(key & 0x0F)] = true;
            keyPressedSinceReset = true;
        }
        public void UnpressKey(byte key)
        {
            keys[(key & 0x0F)] = false;
        }

        public void Load(byte[] data)
        {
            data.CopyTo(ram, 0x200);
        }

        // pass in # of steps per second, eg. 60 steps per second.
        public CPU(int cpustepspersecond)
        {
            this.millisecondsPerCPUStep = (1000.0 / Math.Min(1000, cpustepspersecond));
            byte[] fonts = new byte[] {
                0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
                0x20, 0x60, 0x20, 0x20, 0x70, // 1
                0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
                0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
                0x90, 0x90, 0xF0, 0x10, 0x10, // 4
                0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
                0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
                0xF0, 0x10, 0x20, 0x40, 0x40, // 7
                0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
                0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
                0xF0, 0x90, 0xF0, 0x90, 0x90, // A
                0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
                0xF0, 0x80, 0x80, 0x80, 0xF0, // C
                0xE0, 0x90, 0x90, 0x90, 0xE0, // D
                0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
                0xF0, 0x80, 0xF0, 0x80, 0x80  // F
            };

            // Load font data into RAM below 512 bytes.
            fonts.CopyTo(this.ram, 0);
            this.stopwatch = new Stopwatch();
            this.Reset();
        }
        public byte[] DebugArray
        {
            get
            {
                byte[] newArray = new byte[ram.Length + 1];
                newArray[0] = 0xFF;
                ram.CopyTo(newArray, 1);
                return newArray;
            }
        }
        public void Reset()
        {
            this.stopwatch.Restart();
            // TODO: reset all data to its default values and move the default values from the declarations.
        }

        // Returns # of steps that have been executed in a call.
        public int Update()
        {
            int steps = 0;

            double total = stopwatch.Elapsed.TotalMilliseconds;

            // add difference in time to both our counters.
            accumulatedMillisecondsCPU += (total - lastMilliseconds);
            accumulatedMillisecondsTimer += (total - lastMilliseconds);

            // timers always run at 60 ticks per second, or ~16.6667 milliseconds per.
            while (accumulatedMillisecondsTimer >= millisecondsPerTimerStep)
            {
                accumulatedMillisecondsTimer -= millisecondsPerTimerStep;
                // Decrement timers but don't let them go below 0.
                delayTimer = (byte)(Math.Max(0, delayTimer - 1));
                soundTimer = (byte)(Math.Max(0, soundTimer - 1));
            }

            // CPU simulation runs independent of timer intervals.  call step for every
            // elapsed interval since last call.
            while (accumulatedMillisecondsCPU >= millisecondsPerCPUStep)
            {
                accumulatedMillisecondsCPU -= millisecondsPerCPUStep;
                steps += 1;    
                this.step();
            }

            lastMilliseconds = total;
            return steps;
        }

        private bool waitingForKeypress = false;
        private void step()
        {
            //grab current opcode and all opcode structures from PC
            if ((pc < 0) || (pc > 0xFFE))
            {
                Console.WriteLine("PC was outside acceptable bounds.");
                System.Environment.Exit(1);
            }
            int opcode = ((ram[pc] << 8) + (ram[pc + 1])) & 0xFFFF;
            int nnn = opcode & 0x0FFF;
            byte kk = (byte)(opcode & 0x00FF);
            int w = (opcode & 0xF000) >> 12;
            int x = (opcode & 0x0F00) >> 8;
            int y = (opcode & 0x00F0) >> 4;
            int z = (opcode & 0x000F);



            // special handling for Fx0A - wait for a key press, store value of the key in VX
            // since this stops execution it has to be inserted before any other processing.
            if (w == 0x0F && kk == 0x0A)
            {
                if (!waitingForKeypress)
                {
                    waitingForKeypress = true;
                    keyPressedSinceReset = false;
                    return;
                }

                else if (keyPressedSinceReset)
                    pc += 2;
                return;
            }

            switch (w)
            {
                case 0x0:
                    if (opcode == 0x00E0) // CLS - Clear the Display
                    {
                        for (int dispx = 0; dispx < 64; dispx++)
                        {
                            for (int dispy = 0; dispy < 32; dispy++)
                            {
                                display[dispx, dispy] = false;
                            }
                        }
                        isDisplayStale = true;
                    }
                    else if (opcode == 0x00EE) // RET - Return from Subroutine
                    {
                        if (s.Count > 0)
                        { 
                            pc = s.Pop();
                        }
                        else
                        {
                            // TODO: emit debug for this
                            Console.WriteLine("Unable to RET from subroutine because stack is empty!");
                        }
                    }
                    break;
                case 0x1:
                    // JMP - Unconditional Jump
                    pc = nnn;
                    break;
                case 0x2:
                    // SUB - Call Subroutine.
                    s.Push(pc);
                    pc = nnn;
                    break;
                case 0x3:
                    // SE - Skip next instruction if register x is equal to kk
                    if (registers[x] == kk)
                    {
                        pc += 2;
                    }
                    break;
                case 0x4:
                    // SNE - Skip next instruction if register x is not equal to kk.
                    if (registers[x] != kk)
                    {
                        pc += 2;
                    }
                    break;
                case 0x5:
                    // SEQ - Skip next instruction if register x is equal to register y.
                    if (registers[x] == registers[y])
                    {
                        pc += 2;
                    }
                    break;
                case 0x6:
                    // SET - set kk into register x.
                    registers[x] = kk;
                    break;
                case 0x7:
                    // INC - Increment register x by kk.
                    registers[x] += kk;
                    break;
                case 0x8:
                    switch (z)
                    {
                        case 0:
                            // LD - Store register y in x.
                            registers[x] = registers[y];
                            break;
                        case 1:
                            // OR - set register x to register x OR register y.
                            registers[x] = (byte)(registers[x] | registers[y]);
                            break;
                        case 2:
                            // AND - set register x to register x AND register y.
                            registers[x] = (byte)(registers[x] & registers[y]);
                            break;
                        case 3:
                            // XOR - set register x to register x XOR register y.
                            registers[x] = Convert.ToByte(registers[x] ^ registers[y]);
                            break;
                        case 4:
                            // ADD - set register x to register x + register y, set register 0xF to carry.
                            try
                            {
                                registers[x] = Convert.ToByte(registers[x] + registers[y]);
                                registers[0xF] = 0x00;
                            }
                            catch (OverflowException)
                            {
                                registers[x] = (byte)(registers[x] + registers[y]);
                                registers[0xF] = 0x01;
                            }
                            break;
                        case 5:
                            // SUB - set register x to register x - register y.  
                            // If result is positive (register x >  register y), set register 0xF to 1.
                            // Otherwise, set register 0xF to 0.
                            registers[0xF] = (byte)((registers[x] > registers[y]) ? 1 : 0);
                            registers[x] = (byte)(registers[x] - registers[y]);
                            break;
                        case 6:
                            // SR - shift right, put overflow (lowest bit) into register 0xF.
                            registers[0xF] = (byte)(registers[x] & 0x01);
                            registers[x] = (byte)(registers[x] >> 1);
                            break;
                        case 7:
                            // SBY - subtract register y from register x.
                            // This differs from SUB because it subtracts register y from x instead of vice versa.
                            // store result in register x. 
                            // if register y > register x, set register 0xF to 1, otherwise 0.
                            registers[0xF] = (byte)((registers[y] > registers[x]) ? 1 : 0);
                            registers[x] = (byte)(registers[y] - registers[x]);
                            break;
                        case 0xE:
                            // SHL - shift left, put overflow (highest bit) into register 0xF.
                            registers[0xF] = (byte)(registers[x] & 0x80);
                            registers[x] = (byte)(registers[x] << 1);
                            break;
                    }
                    break;
                case 0x9:
                    //SNY - skip if register x != register y.
                    if (registers[x] != registers[y])
                        pc += 2;
                    break;
                case 0xA:
                    // LDI - Set i pointer to nnn.
                    i = nnn;
                    break;
                case 0xB:
                    // JPN - Jump to nnn + register 0.
                    pc = nnn + registers[0];
                    break;
                case 0xC:
                    // RND - use kk as a mask for a randomly generated byte and store in register x.
                    Random r = new Random();
                    byte[] result = new byte[1];
                    r.NextBytes(result);
                    registers[x] = (byte)(result[0] & kk);
                    break;
                case 0xD:
                    // Draw n-byte sprite stored in memory at i, at coordinates (register x, register y).
                    // use XOR.  if any pixels are erased register 0xF = 1, otherwise 0. 
                    // sprites wrap around the display.
                    // TODO: implement drawing.
                    // Mark display stale so it will be re-calculated.
                    isDisplayStale = true;
                    break;
                case 0xE:
                    switch(kk)
                    {
                        case 0x9E:
                            // SKP - skip next instruction if key with the value in register x is pressed.
                            // TODO: implement keyboard input.
                            break;
                        case 0xA1:
                            // SKN - skip next instruction if key with the value in register x is not pressed.
                            // TODO: implement keyboard input.
                            // HACK: assume all keys are not pressed for now.
                            pc += 2;
                            break;
                    }
                    break;
                case 0xF:
                    switch (kk)
                    {
                        case 0x07:
                            // LDT - load delay timer into register x.
                            registers[x] = delayTimer;
                            break;
                        case 0x0A:
                            // KP - wait for keypress.  This is handled outside of this structure
                            //since it has to handle stopping all execution.
                            // TODO: remove this (used for debugging).
                            Console.WriteLine("Fx0A pressed!");
                            break;
                        case 0x15:
                            // SDT - set delay timer to value in register x.
                            delayTimer = registers[x];
                            break;
                        case 0x18:
                            // SST - set sound timer to value in register x.
                            soundTimer = registers[x];
                            break;
                        case 0x1E:
                            // SEI - set i to register x + i.
                            i = i + registers[x];
                            break;
                        case 0x29:
                            // SIF - Set i to location of sprite for font for digit stored in register x.
                            // fonts are stored at 0x00, and are 5 bytes each.
                            i = registers[x] * 5;
                            break;
                        case 0x33:
                            // BCD - load BCD representation of register x in i, i+1, i+2.
                            // TODO: i'm assuming i doesn't get incremented.  check assumption.
                            var temp = registers[x];
                            for (int o = 2; o >= 0; o--)
                            {
                                ram[i + o] = (byte)(temp % 10);
                                temp /= 10;
                            }
                            break;
                        case 0x55:
                            // MVR - move registers into memory starting at location i.
                            var offset = i;
                            for (int o = 0; o<x; o++)
                            {
                                ram[offset] = registers[o];
                                offset += 1;
                            }
                            break;
                        case 0x65:
                            // RDR - read registers from memory starting at location i.
                            var offset2 = i;
                            for (int o = 0; o<x; o++)
                            {
                                registers[o] = ram[offset2];
                                offset2 += 1;
                            }
                            break;
                    }
                    break;
            }
            // increment PC to next instruction.
            pc += 2;
        }
    }


}
