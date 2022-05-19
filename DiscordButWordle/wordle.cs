using System;
using System.Collections.Generic;
using System.Linq;

namespace WordleBot
{
    class wordle
    {
        public DateTime startDate;
        public int WordleNum = 1;
        public int minUnload = 1;
        public List<string> wordlist = new List<string>();
        public List<string> legalwords = new List<string>();
        public string GreenSquare = ":green_square:";
        public string YellowSquare = ":yellow_square:";
        public string BlackSquare = ":black_large_square:";
        public string CharStart = ":regional_indicator_";
        public result evalGuess(string guess)
        {
            string[] tempHolder = new string[5];
            string word = wordlist[WordleNum];

            int Correct = 0;
            //check for correctly placed letters
            for (int i = 0; i <= guess.Length - 1; i++)
            {
                if (guess[i] == word[i])
                {
                    word = word.Remove(i, 1).Insert(i, "_");
                    tempHolder[i] = GreenSquare;
                    Correct++;
                }
            }
            //Check for incorrectly placed and wrong letters
            for (int i = 0; i <= guess.Length - 1; i++)
            {
                if (tempHolder[i] == null)
                {
                    if (word.Contains(guess[i]))
                    {
                        int index = word.IndexOf(guess[i]);
                        word = word.Remove(index, 1).Insert(index, "_");
                        tempHolder[i] = YellowSquare;
                    }
                    else
                    {
                        tempHolder[i] = BlackSquare;
                    }
                }

            }

            result res = new result();

            for (int i = 0; i < guess.Length; i++)
            {
                res.resultGuess += CharStart + guess[i] + ": ";
            }

            for (int i = 0; i < tempHolder.Length; i++)
            {
                res.resultText += tempHolder[i] + " ";
            }
            if (Correct == 5) { res.isSolved = true; }

            return res;
        }

        public bool validWord(string word)
        {
            if (word.Length == 5 && (wordlist.Contains(word) || legalwords.Contains(word)))
            {
                return true;
            }
            return false;
        }
    }

    class result
    {
        public string resultGuess;
        public string resultText;
        public bool isSolved;
    }

    class playerdata
    {
        public ulong PlayerID;
        public int lastSolved;
        public int highStreak;
        public int currStreak;

        public int currentSession;
        public List<string> sessionGuesses = new List<string>();
        public List<string> sessionBlocks = new List<string>();
        public bool isSolved;

        public int[] SolveCounter = new int[6];
        public DateTime LastAction;
    }


}
