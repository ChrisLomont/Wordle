# Wordle tools

Software to play the popular (2022) online word guessing game Wordle (https://www.powerlanguage.co.uk/wordle/)

The bot achieves under 3.5 moves on average over all 2315 possible games, solves all in 5 or less moves, and only about 2% use 5 moves.

There are a lot of analysis items, like starting word analysis, the ability to run the bot over all games in about 1 minute (multithreaded solver).

Careful analysis went into the pieces to ensure the single and multithreaded items achieve the same results.

## Build

Visual Studio 2022, C#, dotnet 6.

## Usage

Command line, options roughly explained. Read the code to see more.