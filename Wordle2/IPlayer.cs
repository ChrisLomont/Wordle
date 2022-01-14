namespace Wordle2;

interface IPlayer
{
    string Start(); // start game, blank for random, else give a hidden word, "quit" to exit

    string Get(); // get guess

    // set result from previous guess
    void Result(uint result);
}