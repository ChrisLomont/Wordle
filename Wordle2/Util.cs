using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Mail;

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
        Knowledge k, 
        bool verbose = false, 
        int guessWordStyle = 0, 
        bool sortOnWorst = false,
        bool multiThreaded = false
        )
    {
        if (verbose)
            Console.WriteLine("Scoring: ");
        
        var left = k.Filter(Words.HiddenWords); // possible words left
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
                var ans1 = DoStep(guess, verbose, left, scoreHelper, k);
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
                var ans1 = DoStep(guess, verbose, left, sc, k);
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

    public static uint Score(string answer, string guess)
    {
#if false
        var ans = ScoreOld(answer, guess);
        uint s = 0;
        for (var i = 0; i < 5; ++i)
        {
            var t = (uint)ans[i];
            Debug.Assert(t<4);
            s += t<<(2*i);
        }

        return s;
#else
        var wordlen = guess.Length;
        // var arr = new Info[wordlen];
        uint score = 0;

        // score perfect ones, mark rest as unused
        for (var i = 0; i < wordlen; ++i)
        {
            Set(ref score, Info.Unused,i);// no match is default
            //arr[i] = Info.Unused; 
            if (guess[i] == answer[i])
            {
                // perfect
                Set(ref score, Info.Perfect, i);
                // arr[i] = Info.Perfect; // perfect
            }
        }

#if true
        //Dictionary<char, int> counts = new Dictionary<char, int>();
        // for speed: count of letters in word, at most 3 of a given letter, so 0-3 in 2 bits
        // 26 letters, 26*2 uses 52 bits, can store in 64 bit counter
        ulong counter = 0UL, mask = 0b0011;
        for (var i = 0; i < wordlen; ++i)
        {
            if (guess[i] != answer[i])
            {
                var c = answer[i];
                counter += 1UL << ((c - 'a') * 2);
                //if (!counts.ContainsKey(c))
                //    counts.Add(c, 0);
                //counts[c]++;
            }
        }

        for (var i = 0; i < wordlen; ++i)
        {
            //if (arr[i] == Info.Unused && counts.ContainsKey(guess[i]) && counts[guess[i]]>0)
            //{
            //    counts[guess[i]]--;
            //    arr[i] = Info.Misplaced;
            //}
            var g2 = (guess[i] - 'a') * 2;
            var m = mask << g2;
            if (Get(score,i) == Info.Unused
                /*arr[i] == Info.Unused */ && (counter & m) != 0)
            {
                counter -= 1UL << g2;
                Set(ref score, Info.Misplaced,i);
                //arr[i] = Info.Misplaced;
            }
        }
#else
        // score imperfect ones, only if they are not matched up to perfect
        for (var i = 0; i < wordlen; ++i)
        {
            for (var j = 0; j < wordlen; ++j)
            {
                if (j != i && guess[i] == answer[j] && arr[i] == Info.Unused)
                    arr[i] = Info.Misplaced; // misplaced
            }
        }
#endif

        return score;

#endif
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

    public static Info[] ScoreOld(string answer, string guess)
    {
        var wordlen = guess.Length;
        var arr = new Info[wordlen];

        // score perfect ones, mark rest as unused
        for (var i = 0; i < wordlen; ++i)
        {
            arr[i] = Info.Unused; // no match is default
            if (guess[i] == answer[i])
                arr[i] = Info.Perfect; // perfect
        }

#if true
        //Dictionary<char, int> counts = new Dictionary<char, int>();
        // for speed: count of letters in word, at most 3 of a given letter, so 0-3 in 2 bits
        // 26 letters, 26*2 uses 52 bits, can store in 64 bit counter
        ulong counter = 0UL, mask = 0b0011;
        for (var i = 0; i < wordlen; ++i)
        {
            if (guess[i] != answer[i])
            {
                var c = answer[i];
                counter += 1UL << ((c - 'a') * 2);
                //if (!counts.ContainsKey(c))
                //    counts.Add(c, 0);
                //counts[c]++;
            }
        }

        for (var i = 0; i < wordlen; ++i)
        {
            //if (arr[i] == Info.Unused && counts.ContainsKey(guess[i]) && counts[guess[i]]>0)
            //{
            //    counts[guess[i]]--;
            //    arr[i] = Info.Misplaced;
            //}
            var g2 = (guess[i] - 'a') * 2;
            var m = mask << g2;
            if (arr[i] == Info.Unused && (counter & m) != 0)
            {
                counter -= 1UL << g2;
                arr[i] = Info.Misplaced;
            }
        }
#else
        // score imperfect ones, only if they are not matched up to perfect
        for (var i = 0; i < wordlen; ++i)
        {
            for (var j = 0; j < wordlen; ++j)
            {
                if (j != i && guess[i] == answer[j] && arr[i] == Info.Unused)
                    arr[i] = Info.Misplaced; // misplaced
            }
        }
#endif

        return arr;
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