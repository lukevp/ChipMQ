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
        private ushort i = 0;

        // start with 16 levels of stack.
        private Stack<ushort> s = new Stack<ushort>(16);

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
            // TODO: implement real step and emulation.
            Random r = new Random();
            for (int i = 0; i < 32; i++)
            {
                display[i] = Convert.ToByte(r.Next() % 256);
            }
        }
    }


}
