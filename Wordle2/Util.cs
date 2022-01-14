using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Mail;
using System.Numerics;

namespace Wordle2;

static class Util
{
    public static void TallyLetters()
    {
        // tally letters, output with counts
        var cnt = new (int, char)[26];
        foreach (var w in Words.HiddenWords)
        foreach (var c in w.Text)
        {
            var i = c - 'a';
            cnt[i] = (cnt[i].Item1 + 1, c);
        }
        Array.Sort(cnt, (a, b) => -a.Item1.CompareTo(b.Item1));
        foreach (var (n, c) in cnt)
        {
            Console.WriteLine($"{c}: {n}");
        }
        // ReSharper disable All StringLiteralTypo
        // top 15: e,a,r,o,t,   l,i,s,n,c,  u,y,d,h,p,   m,g,b,f,k,  w,v,z,x,q,j
        // earot + lisnc = orate, -----
        // t over: earol + tisnc = realo + ______
        // t for s: arose + clint (not in small dict)
        // t for c: + lints
        // ReSharper enable All StringLiteralTypo

    }

    public static (List<Ans> bestAvg, List<Ans> bestWorst) BestFirstWord()
    {
        // find best start word
        var ans = ScoreAll(
            new Knowledge(), 
            verbose: false, 
            guessWordStyle: 2,
            multiThreaded:true
        );
        List<Ans> bestAvg = new();
        List<Ans> bestWorst = new();
        bestAvg.AddRange(SortAns(ans, Words.HiddenWords, sortOnWorst: false));
        bestWorst.AddRange(SortAns(ans, Words.HiddenWords, sortOnWorst: true));

        Console.WriteLine("Best start by worst outcome");
        foreach (var a in bestWorst.Take(20))
            Console.WriteLine($"{a.Word} {a.Avg:F2} {a.Worst}, ");

        Console.WriteLine("Best start by avg outcome");
        foreach (var a in bestAvg.Take(20))
            Console.WriteLine($"{a.Word} {a.Avg:F2} {a.Worst}, ");
        return (bestAvg, bestWorst);
    }

    public static void CheckLetterAssumptions()
    {
        // letter count
        foreach (var w in Words.AllWords)
        {
            int[] cnt1 = new int[26];
            foreach (var c in w.Text)
                cnt1[c - 'a']++;
            foreach (var i in cnt1)
                Trace.Assert(i < 4); // needed for quick bitwise scoring
        }
    }

    class ScoreHelper
    {
        public Knowledge k2 = new();
        public List<Ans> items = new();
    }

    // given knowledge, see how each possible word guess splits the unknowns down
    // guess words: 0 = use those possible, 1 = use all possible hidden, 2 = use all words
    // sort on worst or on avg score when ordering words
    public static List<Ans> ScoreAll(
        Knowledge knowledge, 
        bool verbose = false, 
        int guessWordStyle = 0, 
        bool sortOnWorst = false,
        bool multiThreaded = false
        )
    {
        if (verbose)
            Console.WriteLine("Scoring: ");
        
        var left = knowledge.Filter(Words.HiddenWords); // possible words left
        var guessable = left;
        if (guessWordStyle == 1)
            guessable = Words.HiddenWords;
        else if (guessWordStyle == 2)
            guessable = Words.AllWords;


        static Ans DoStep(Word guess, bool verbose, List<Word> left, ScoreHelper sc, Knowledge k)
        {

            if (verbose)
                Console.WriteLine($"Guessing {guess}: ");

            // count stats
            int countTotal = 0, countMax = 0;

            foreach (var hidden in left)
            {
                sc.k2.Copy(k);
                var info = Score(hidden, guess);
                sc.k2.Add(guess.Text, info);
                var count = sc.k2.Filter(left).Count;
                if (count == 0)
                {
                    Console.WriteLine(sc.k2.Filter(left).Count);
                }
                Trace.Assert(count != 0);
                countTotal += count;
                countMax = Math.Max(count, countMax);
                if (verbose)
                    Console.WriteLine($"    {hidden}->{count}");
            }
            var avg = (float)countTotal / left.Count;
            return new(guess, avg, countMax);
        }

        List<Ans> items = new();

        if (multiThreaded)
        {

            // need thread partition with per thread copy of Knowledge k2
            // https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-write-a-parallel-foreach-loop-with-partition-local-variables
            object locker = new(); // for final aggregation
            // first is type of collection
            // second is type of thread local
            Parallel.ForEach<Word,ScoreHelper>(
                guessable, // source collection
                ()=>new ScoreHelper(), // initialize the local
                (guess,_, scoreHelper) => // method
                {
                var ans1 = DoStep(guess, verbose, left, scoreHelper, knowledge);
                scoreHelper.items.Add(ans1);
                return scoreHelper; // used next pass
                },
                (scoreHelper) =>
                {
                    lock (locker)
                    {
                        items.AddRange(scoreHelper.items);
                    }
                } // finalize method, called on each knowledge kk from the loops
            );
        }
        else
        {
            var sc = new ScoreHelper();

            foreach (var guess in guessable)
            {
                var ans1 = DoStep(guess, verbose, left, sc, knowledge);
                items.Add(ans1);
            }
        }

        items = SortAns(items, left, sortOnWorst);
        return items;
    }

    static List<Ans> SortAns(List<Ans> ans, List<Word> hiddenWordsLeft, bool sortOnWorst)
    {
        if (sortOnWorst)
        {

            // sort by lowest worst, break ties towards words in 'left'
            // todo - also break ties by average?
            ans.Sort((a, b) =>
                {
                    var c = a.Worst.CompareTo(b.Worst);
                    if (c == 0)
                    {
                        var aGood = hiddenWordsLeft.Contains(a.Word);
                        var bGood = hiddenWordsLeft.Contains(b.Word);
                        if (aGood && !bGood) return -1; // a best
                        if (!aGood && bGood) return 1; // b best
                        if (aGood && bGood)
                        {
                            var c2 = a.Avg.CompareTo(b.Avg); // sort on avg
                            if (c2 != 0) return c2;
                        }
                        return a.Word.Text.CompareTo(b.Word.Text); // final tie breaker
                    }

                    return c;
                }
            );
        }
        else
        {
            // sort by lowest avg, break ties towards words in 'left'
            // todo - also break ties by average?
            ans.Sort((a, b) =>
                {
                    var c = a.Avg.CompareTo(b.Avg);
                    if (c == 0)
                    {
                        var aGood = hiddenWordsLeft.Contains(a.Word);
                        var bGood = hiddenWordsLeft.Contains(b.Word);
                        if (aGood && !bGood) return -1; // a best
                        if (!aGood && bGood) return 1; // b best
                        if (aGood && bGood)
                        {
                            var c2 = a.Worst.CompareTo(b.Worst); // sort on worst
                            if (c2 != 0) return c2;
                        }
                        return a.Word.Text.CompareTo(b.Word.Text); // final tie breaker
                    }

                    return c;
                }
            );
        }

        return ans;
    }

    static Util()
    {
        Words.RequireInit();
        var hiddenCount = Words.HiddenWords.Count;
        var allCount = Words.AllWords.Count;
        ScoreCache = new uint[hiddenCount*allCount];

        // sanity check
        HashSet<string> seen = new();
        foreach (var w in Words.HiddenWords)
        {
            if (seen.Contains(w.Text))
                Console.WriteLine($"Double word {w}");
            seen.Add(w.Text);
        }
        Console.WriteLine($"Word count {Words.HiddenWords.Count}");
    }


    // word to word score caching for speed
    // THREADING: in C#, read/write of uint is guaranteed atomic, so this multithreadable
    static readonly uint[] ScoreCache;

    public static uint ToScore(params Info[] info)
    {
        uint s = 0;
        for (var i = 0; i < 5; ++i)
        {
            var t = ((uint)(info[i]) << (2 * i));
            s += t;

        }

        return s;
    }

    // given two strings, compute the match array between them
    // nuances for multiple letters....
    // 5 slots, each 4 answers, = 20 bits
    // first index is lowest bits
    
    public static uint Score(Word answer, Word guess)
    {
        var allCount = Words.AllWords.Count;
        var hiddenCount = Words.HiddenWords.Count;
        Trace.Assert(answer.Index < hiddenCount);
        Trace.Assert(guess.Index < allCount);
        long index = answer.Index * allCount + guess.Index;
        var score = ScoreCache[index];
        if (score == 0) // none are ever 0
        {
            score = Score(answer.Text, guess.Text) + 1; // 1 offset in cache
            ScoreCache[index] = score;
        }

        return score - 1;
    }

    public static uint Score(string solutionWord, string guessedWord)
    {
        uint guessUsed = 0; // flags
        uint answerUsed = 0;
        
        Debug.Assert((int)Info.Unused == 2); // next line assumes all set to Unused, else need to set
        uint score = 0b0101010101U * (uint)Info.Unused; // all marked Unused, 2 bits each

        for (var i = 0; i < guessedWord.Length; i++)
        {
            if (guessedWord[i] == solutionWord[i])
            {
                guessUsed |= 1U << i;
                answerUsed |= 1U << i;
                Set(ref score, Info.Perfect, i); // todo - for speed, could make this a shifted xor mask
            }
        }

        for (var gIndex = 0; gIndex < guessedWord.Length; gIndex++)
        {
            if (((guessUsed >> gIndex) & 1) == 0)
            {
                var guessLetter = guessedWord[gIndex];
                for (var aIndex = 0; aIndex < solutionWord.Length; aIndex++)
                {
                    if (((answerUsed >> aIndex) & 1) == 0 && guessLetter == solutionWord[aIndex])
                    {
                        Set(ref score, Info.Misplaced, gIndex);
                        answerUsed |= 1U << aIndex;
                        break;
                    }
                }
            }
        }
        return score;
    }

    static Info Get(uint score, int i)
    {
        return (Info)((score >> (2 * i)) & 3);
    }
    
    static void Set(ref uint score, Info q, int i)
    {
        i = 2 * i;
        score &= (uint)(~(0b11 << i));
        score |= ((uint)q) << i;
    }

    public static void Write(string guess, uint result)
    {
        var (b, f) = (Console.BackgroundColor, Console.ForegroundColor);
        Console.ForegroundColor = ConsoleColor.Black;
        for (var i = 0; i < guess.Length; ++i)
        {
            var t = (Info)((result >> (2 * i)) & 3);
            Console.BackgroundColor = t switch
            {
                Info.Unused => ConsoleColor.Gray,
                Info.Perfect => ConsoleColor.Green,
                Info.Misplaced => ConsoleColor.Yellow,
                _ => throw new NotImplementedException()
            };
            Console.Write(guess[i]);
        }

        Console.BackgroundColor = b;
        Console.ForegroundColor = f;

    }

}