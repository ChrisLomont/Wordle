# Wordle tree

How to brute force best trees for smallish subtrees



```
Start with some knowledge, say k0, some start index i0 to look over guess words, current depth d0, max depth to search M

return depth needed, and next word to get there.
h0 = k0.filter(possible hidden words)


(depth needed, best next guess) Recurse(k0,i0,d,M, h0)
{

if (d>=M) return infinity this path bad

if (h0.count == 1) return (d,??)


// want to find guessword that has lowest of max required depths, 
// and/or word with lowest avgs

// for each word, find how deep required to find all answers
// things off the end get infinity score
bestScore = infinity
bestWord = ""

for i1 = i0 to # guesswrods
   g0 =  guesswords[i1]
   S = set of possible scores for g0 over h0 (want those adding knowledge.. want those shrinking h0 size?)
   worst = 0
   for s,k1,h1 in S  
      // h1 = k1.filter(possible hidden words), k1 = k0 + g0,s      
      if (h1.count == 1)
           depthNeeded is d+1 // we will guess the correct word next depth
      else
          (depthNeeded, _) = Recurse(k1,i1+1,d+1,M,h1)
      worst = max(worst,depthNeeded)
   if (worst<bestScore)
      // this guess best guess
      bestScore = worst
      bestWord = guess
return (bestScore, bestWord)   
}



```



