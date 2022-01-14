using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.CompilerServices;

namespace Wordle2
{
    // work on search of tree
    internal class Tree
    {
        public class Node
        {
            public override string ToString()
            {
                return $"{children.Count}, {possibleHiddenWords?.Count}";
            }

            // THREAD - must be done on single thread or dealt with differently
            public static int nodeCount = 0;

            public Node()
            {
                nodeCount++;
                if ((nodeCount % 100000) == 0)
                    Console.WriteLine($"Node count {nodeCount}");
            }

            public int depth; // root is 0
            public Knowledge? k;
            public List<Word>? possibleHiddenWords;
            public List<Node> children = new();
            public Node? parent = null;
        }

        public Node root;



        public void Build(IEnumerable<Word> possibleHiddenWords, params Word[] guessedOrder)
        {
            root = new Node { k = new(), possibleHiddenWords = new() };
            root.possibleHiddenWords.AddRange(possibleHiddenWords);
            var unprocessed = new Queue<Node>();
            unprocessed.Enqueue(root);
            var curDepth = 0;
            long nodesProcessed = 0, sameKnowledge = 0;

            while (unprocessed.Any())
            {
                var node = unprocessed.Dequeue();
                ++nodesProcessed;
                if (node.depth > curDepth)
                {
                    curDepth = node.depth;
                    Console.WriteLine($"Node depth to {curDepth} at {nodesProcessed}");
                }

                if (node.possibleHiddenWords.Count < 2)
                    continue; // done
                var guesses = Words.AllWords;
                if (node.depth < guessedOrder.Length)
                    guesses = new() { guessedOrder[node.depth] };

                foreach (var hiddenWord in node.possibleHiddenWords)
                    foreach (var guess in guesses)
                    {
                        var k = new Knowledge();
                        k.Copy(node.k);
                        k.Add(guess.Text, Util.Score(hiddenWord, guess));
                        var newPossible = k.Filter(node.possibleHiddenWords);
                        if (newPossible.Count < node.possibleHiddenWords.Count)
                        {
                            // see if any other kids have same knowledge, if so, this word points there too?
                            var same = false;
                            foreach (var c in node.children)
                                if (c.k.Same(k))
                                {
                                    same = true;
                                    ++sameKnowledge;
                                    break;
                                }

                            //same = false;
                            if (!same)
                            {

                                // made progress

                                var child = new Node
                                {
                                    k = k,
                                    possibleHiddenWords = newPossible,
                                    depth = node.depth + 1,
                                    parent = node
                                };

                                node.children.Add(child);
                                unprocessed.Enqueue(child);
                            }
                        }

                    }
            }
        }
    }
}
