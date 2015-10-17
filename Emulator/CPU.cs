using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emulator
{

    public class CPU
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

        private int delayTimer = 0;
        private int soundTimer = 0;

        // 64 x 32 pixel display - but it's only black / white so we are bit packing the X axis into 32/8 bytes = 5 bytes per, * 64 columns.
        private byte[] display = new byte[64 * 5];

        private double millisecondsPerCPUStep;
        private double millisecondsPerTimerStep = (1000.0 / 60);

        private Stopwatch stopwatch;

        private double lastMilliseconds = 0;
        private double accumulatedMillisecondsCPU = 0;
        private double accumulatedMillisecondsTimer = 0;
        
        public string GetCompressedDisplay()
        {
            return Convert.ToBase64String(display);
        }

        // pass in # of steps per second, eg. 60 steps per second.
        public CPU(int stepspersecond)
        {
            this.millisecondsPerCPUStep = (1000.0 / Math.Min(1000, stepspersecond));
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

        public void Reset()
        {
            this.stopwatch.Restart();
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
                delayTimer = Math.Max(0, delayTimer - 1);
                soundTimer = Math.Max(0, soundTimer - 1);
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

        private void step()
        {
            // for now, just randomly set some areas of the screen.

            //grab current opcode from PC

            int opcode = (ram[pc] << 8 + ram[pc + 1]) & 0xFFFF;
            int nnn = opcode & 0x0FFF;
            int kk = opcode & 0x00FF;
            int w = opcode & 0xF000;
            int x = opcode & 0x0F00;
            int y = opcode & 0x00F0;
            int n = opcode & 0x000F;

            switch (w)
            {
                case 0x0:
                    if (opcode == 0x00E0) // CLS - Clear the Display
                    {
                        this.display = new byte[64 * 5];
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
                    break;
                case 0x7:
                    break;
                case 0x8:
                    break;
                case 0x9:
                    break;
                case 0xA:
                    break;
                case 0xB:
                    break;
                case 0xC:
                    break;
                case 0xD:
                    break;
                case 0xE:
                    break;
                case 0xF:
                    break;
            }
            
            Random r = new Random();
            for (int i = 0; i < 32; i++)
            {
                display[i] = Convert.ToByte(r.Next() % 256);
            }
        }
    }


}
