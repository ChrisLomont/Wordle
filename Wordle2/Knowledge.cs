using System.Collections.Concurrent;

namespace Wordle2;

// track known things about words
public class Knowledge
{
    #region State
    // 130+130+26+26 = 312 bits needed for state

    // bit i + 5*j set for letter 'a'+j in slot i
    readonly Bits correctIndices = new();

    // bit i + 5*j set for letter 'a'+j in slot i
    readonly Bits misplacedIndices = new();

    // bit i set if letter 'a'+i used but misplaced
    uint misplacedUsedBitFlags;

    // bit i set if letter 'a'+i unused
    uint unusedBitFlags;

    #endregion

    #region Hash count

    //THREAD - safe
    static readonly ConcurrentDictionary<ulong, byte> SeenHash = new();

    static long trackedCount;

    public static void DumpHashInfo()
    {
        var collisionCount = trackedCount - SeenHash.Count;
        Console.WriteLine($"Unique hashes {SeenHash.Count}, collisions {collisionCount}");
    }

    // see how many unique knowledge items form
    static void TrackHash(Knowledge k)
    {
        return; // disable for speed
        Interlocked.Increment(ref trackedCount);
        var hash = k.GenHash();
        SeenHash.AddOrUpdate(hash, 0, (_, _) => 0);
    }
    #endregion

    ulong GenHash()
    {
        var hash = correctIndices.GenHash();
        Rotate(ref hash, 41, 0x18974567);
        hash ^= misplacedIndices.GenHash();
        Rotate(ref hash, 21, 238764239234071);
        hash ^= this.misplacedUsedBitFlags;
        Rotate(ref hash, 53, 32874602378403897UL);
        hash ^= this.unusedBitFlags;
        return hash;

        void Rotate(ref ulong v, int rotate, ulong multiplier)
        {
            v = v * multiplier;
            v = (v << rotate) ^ (v >> (64 - rotate));
        }
    }

    /// <summary>
    /// copy into this one
    /// </summary>
    /// <param name="k"></param>
    public void Copy(Knowledge k)
    {
        correctIndices.Copy(k.correctIndices);
        misplacedIndices.Copy(k.misplacedIndices);
        misplacedUsedBitFlags = k.misplacedUsedBitFlags;
        unusedBitFlags = k.unusedBitFlags;
    }

    public void Add(string text, uint score)
    {
        for (var i = 0; i < text.Length; ++i)
        {
            var t = (Info)((score >> (2 * i)) & 3);
            if (t == Info.Perfect)
                SetCorrect(text[i], i);
            else if (t == Info.Misplaced)
                SetMisplaced(text[i], i);
            else if (t == Info.Unused)
                SetUnused(text[i]);
            else throw new NotImplementedException();
        }
        TrackHash(this);
    }

    public override string ToString()
    {
        var unu = "";
        for (var i = 0; i < 26; ++i)
            if ((unusedBitFlags & (1U << i)) != 0)
                unu += (char)('a' + i);

        var c = new[] { '_', '_', '_', '_', '_' };
        for (var i = 0; i < 26; ++i)
        {
            for (var j = 0; j < 5; ++j)
                if (correctIndices.Get(i * 5 + j) != 0)
                {
                    c[j] = (char)('a' + i);
                }
        }
        var co = c.Aggregate("", (a, b) => a + b);



        var msg = $"correct {co}, unused {unu}: ";
        // todo- misplaced list
        //foreach (var (ch, i) in misplacedList)
        //    msg += $"{ch},{i} ";
        return msg;

    }



    // filter words
    public List<Word> Filter(IEnumerable<Word> words) => words.Where(WordPossible).ToList();

    // filter to only words that add knowledge, requires knowing answer
    public List<Word> AddsKnowledgeFilter(IEnumerable<Word> words, Word answer)
    {
        return words.Where(w =>
            {
                var k = new Knowledge();
                k.Copy(this);
                k.Add(w.Text, Util.Score(answer, w));
                return !Same(k); // want them to differ, added knowledge
            }
            ).ToList();
    }


    public bool Same(Knowledge k)
    {
        // check fields:
        return
            k.correctIndices.Same(correctIndices) &&
            k.misplacedIndices.Same(misplacedIndices) &&
            k.misplacedUsedBitFlags == misplacedUsedBitFlags &&
            k.unusedBitFlags == unusedBitFlags;
    }


    /// <summary>
    /// return true if this word possible
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    public bool WordPossible(Word word)
    {
        var b1 = correctIndices.TestAnd(word.CorrectIndices); // has correct ones in place
        var b2 = (word.UsedBitFlags & unusedBitFlags) == 0; // no unused ones in play
        var b3 = (word.UsedBitFlags & misplacedUsedBitFlags) == misplacedUsedBitFlags; // all misplaced ones occur
        var b4 = b1 & b2 & b3;// passed so far
        if (!b4) return false; // early cutoff

        // letter j cannot be at position i - bitmask
        return misplacedIndices.TestAndZero(word.CorrectIndices);
    }

    void SetUnused(char letter)
    {
        // don't set unused if already marked somewhere...
        for (var index = 0; index < 5; ++index)
        {
            var i = index + 5 * (Char.ToLower(letter) - 'a');
            if (correctIndices.Get(i) != 0) return;
        }
        if ((misplacedUsedBitFlags & (1U << (letter - 'a'))) != 0)
            return;
        unusedBitFlags |= 1U << (Char.ToLower(letter) - 'a');
    }
    /// <summary>
    /// SetCorrect a letter in a position
    /// </summary>
    /// <param name="letter"></param>
    /// <param name="index"></param>
    void SetCorrect(char letter, int index)
    {
        unusedBitFlags &= ~(1U << (Char.ToLower(letter) - 'a'));
        var i = (index % 5) + 5 * (Char.ToLower(letter) - 'a');
        correctIndices.Set(i, 1);
    }


    /// <summary>
    /// Mark letter as not in position
    /// </summary>
    /// <param name="letter"></param>
    /// <param name="index"></param>
    void SetMisplaced(char letter, int index)
    {
        unusedBitFlags &= ~(1U << (Char.ToLower(letter) - 'a'));
        var j = letter - 'a';

        misplacedUsedBitFlags |= 1U << j;

        // letter j cannot be in slot index
        misplacedIndices.Set(j * 5 + index, 1); // set one
    }
}

public enum Info
{
    Perfect,   // placed perfectly
    Misplaced, // used, wrong slot
    Unused,    // not used at all
}