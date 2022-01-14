using System.Diagnostics;

namespace Wordle2;

public class Bits
{
    // same values?
    public bool Same(Bits b)
    {
        for (var i = 0; i < BitField.Length; ++i)
            if (b.BitField[i] != BitField[i])
                return false;
        return true;
    }

    public ulong GenHash()
    {
        ulong v = 0;
        foreach (var b in BitField)
        {
            v = v * 9812768716291701UL + b; // todo - bad hash maybe
        }

        return v; 
    }


    /// <summary>
    /// return bits & this == this
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public bool TestAnd(Bits b)
    {
        for (var i = 0; i < this.BitField.Length; ++i)
        {
            if ((b.BitField[i] & this.BitField[i]) != this.BitField[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// return bits & this == 0
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public bool TestAndZero(Bits b)
    {
        for (var i = 0; i < this.BitField.Length; ++i)
        {
            if ((b.BitField[i] & this.BitField[i]) != 0)
                return false;
        }
        return true;
    }

    public ulong[] BitField = new ulong[3];

    public void Copy(Bits b)
    {
        Array.Copy(b.BitField, 0, BitField, 0, BitField.Length);
    }

    /// <summary>
    ///  set bit at index to value
    /// </summary>
    /// <param name="index"></param>
    /// <param name="val"></param>
    public void Set(int index, int val)
    {
        Trace.Assert(0 <= index && index < BitField.Length * 64);
        val &= 1; // 0,1
        var i = index / 64;
        index &= 63;

        if (val == 1)
            BitField[i] |= 1UL << index;
        else
            BitField[i] &= ~(1UL << index);
    }
    public int Get(int index)
    {
        Trace.Assert(0 <= index && index < BitField.Length * 64);
        var i = index / 64;
        index &= 63;

        return (int)((BitField[i] >> index) & 1);
    }
}