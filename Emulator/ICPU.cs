using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emulator
{
    public interface ICPU
    {
        #region Properties For External Use (via MQ, etc.)

        byte[] DisplayArray { get; }
        byte[] DebugArray { get; }

        #endregion

        #region Methods for interacting with CPU

        void PressKey(byte key);
        void UnpressKey(byte key);

        // Resets the CPU.
        void Reset();
        // Loads a rom into the CPU at 0x200.
        void Load(byte[] romData);

        // Returns # of steps that have been executed in a call.
        int Update();

        #endregion
    }
}