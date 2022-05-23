using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Linq;

namespace WordleBot
{
    class Program
    {
        public static string TOKEN;
        public List<ulong> OwnerIDs = new List<ulong>();
        public wordle Game = new wordle();

        public List<ulong> activeIDs = new List<ulong>();
        public Dictionary<ulong, playerdata> activePlayers = new Dictionary<ulong, playerdata>();
        public char Prefix = '?';


        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;

        public async Task MainAsync()
        {
            if (!File.Exists("data\\token.txt")) { Console.WriteLine("there wasn't found a token file"); Console.Read(); }
            TOKEN = File.ReadAllText("data\\token.txt");
            using (StreamReader SR = new StreamReader("data\\ownerid.txt"))
            {
                while (!SR.EndOfStream)
                {
                    OwnerIDs.Add(ulong.Parse(SR.ReadLine()));
                }
            }

            Game = JsonConvert.DeserializeObject<wordle>(File.ReadAllText("data\\gamedata.json"));
            if ((DateTime.UtcNow - Game.startDate).Days != Game.WordleNum)
            {
                Game.WordleNum = (DateTime.UtcNow - Game.startDate).Days;
            }

            if (!Directory.Exists("data\\userdata")) { Directory.CreateDirectory("data\\userdata"); }


            _client = new DiscordSocketClient();
            _client.Log += Log;
            _client.MessageReceived += CommandHandler;

            await _client.LoginAsync(TokenType.Bot, TOKEN);
            await _client.StartAsync();

            while (true)
            {
                int seconds = 0;
                while (seconds < 10)
                {
                    Thread.Sleep(1000);
                    seconds++;
                }

                if ((DateTime.UtcNow - Game.startDate).Days != Game.WordleNum)
                {
                    Game.WordleNum = (DateTime.UtcNow - Game.startDate).Days;
                }

                //CHECK IF WE HAVE PLAYERS IN MEMORY WE CAN UNLOAD
                for (int i = activeIDs.Count - 1; i >= 0; i--)
                {
                    activePlayers.TryGetValue(activeIDs[i], out playerdata player);
                    if (player.LastAction.AddMinutes(Game.minUnload) < DateTime.UtcNow)
                    {
                        savePData(player);
                        activePlayers.Remove(activeIDs[i]);
                        activeIDs.RemoveAt(i);
                    }
                }

            }
        }

        void savePData(playerdata data)
        {
            File.WriteAllText($"data\\userdata\\{data.PlayerID}.json", JsonConvert.SerializeObject(data));
        }


        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg.Message);
            return null;
        }

        private async Task CommandHandler(SocketMessage msg)
        {
            if (msg.Author.IsBot) { return; }

            //Setup commands

            if (msg.Content.StartsWith("?"))
            {

                string command = command = msg.Content.Substring(1, msg.Content.Length - 1).ToLower();
                if (msg.Content.IndexOf(' ') != -1)
                {
                    command = msg.Content.Substring(1, msg.Content.IndexOf(' ') - 1).ToLower();
                }

                switch (command)
                {
                    case "rem":
                        if (!isOwner(msg.Author.Id))
                        {
                            await wrongPrivelege(msg.Channel);
                            return;
                        }
                        removeUser(ulong.Parse(msg.Content.Substring(msg.Content.IndexOf(' '), msg.Content.Length - msg.Content.IndexOf(' ')).Trim()));
                        return;
                    //Check if user is an owner and close program gracefully
                    case "quit":
                        if (!isOwner(msg.Author.Id))
                        {
                            await wrongPrivelege(msg.Channel);
                            return;
                        }
                        await stopBot(msg.Channel);
                        return;
                    //Make a guess
                    case "g":
                        string Word = msg.Content.Substring(msg.Content.IndexOf(' '), msg.Content.Length - msg.Content.IndexOf(' ')).Trim();
                        playerdata Player = GetPlayer(msg.Author.Id);

                        //Check if user is on the correct wordle
                        updatePlayer(Player);

                        //Check if user is able to make a guess
                        //Check if word is allegable
                        if (await doChecks(Player, Word, msg.Channel))
                        {
                            //Do all ze logic
                            await MakeGuess(Player, msg, Word);
                        }
                        return;
                    //reply with the days word
                    case "word":
                        if (!isOwner(msg.Author.Id))
                        {
                            await wrongPrivelege(msg.Channel);
                            return;
                        }
                        await msg.Channel.SendMessageAsync("todays word is -> " + Game.wordlist[Game.WordleNum]);
                        return;
                    //Reply with users stats
                    case "stats":
                        await DisplayStats(msg.Author, GetPlayer(msg.Author.Id), msg.Channel);
                        return;
                    //Reply with help commands
                    case "help":
                        await HelpPage(msg.Channel, msg.Author.Id);
                        return;
                    case "rules":
                        await RulePage(msg.Channel);
                        return;
                    case "result":
                        await msg.Channel.SendMessageAsync(GetResult(GetPlayer(msg.Author.Id)));
                        return;
                }
            }
        }

        private string GetResult(playerdata Player)
        {
            string Text = "";
            if (Player.isSolved)
            {
                Text = "Wordle " + (Game.WordleNum + 1) + " " + Player.sessionGuesses.Count + "/6\r\n";
                for (int i = 0; i < Player.sessionGuesses.Count; i++)
                {
                    Text += Player.sessionBlocks[i] + "\r\n";
                }
                return Text;
            }
            else if (Player.sessionGuesses.Count == 6)
            {
                Text = "Wordle " + (Game.WordleNum + 1) + " X/6\r\n";
                for (int i = 0; i < Player.sessionGuesses.Count; i++)
                {
                    Text += Player.sessionBlocks[i] + "\r\n";
                }
                return Text;
            }
            return "you haven't finished the wordle";
        }

        private void removeUser(ulong id)
        {
            if (activeIDs.Contains(id))
            {
                activeIDs.Remove(id);
                activePlayers.Remove(id);
            }
            if (File.Exists($"data\\userdata\\{id}.json")) { File.Delete($"data\\userdata\\{id}.json"); }
        }

        private async Task RulePage(ISocketMessageChannel channel)
        {
            EmbedBuilder stats = new EmbedBuilder();
            stats.Color = Color.Red;
            stats.AddField(CreateField("How to play", "Guess the wordle in six tries.\r\nEach guess must be a valid five-letter word.\r\nAfter each guess, the color of the blocks indicate how close your guess was to the word.\r\nThere's a new wordle every day"));
            stats.AddField(CreateField($"Green block {Game.GreenSquare}", "indicates that the letter is in the word and in the correct place"));
            stats.AddField(CreateField($"Yellow block {Game.YellowSquare}", "indicates that the letter is in the word but in a different place"));
            stats.AddField(CreateField($"Black block {Game.BlackSquare}", "indicates that the letter is not in the word"));
            stats.AddField(CreateField("Time untill next wordle", GetNextTime()));
            stats.AddField(CreateField("To see a list of commands use \"?help\""));
            await channel.SendMessageAsync("", false, stats.Build());

        }

        private async Task MakeGuess(playerdata Player, SocketMessage msg, string word)
        {
            Player.LastAction = DateTime.UtcNow;
            result res = Game.evalGuess(word);
            Player.sessionGuesses.Add(res.resultGuess);
            Player.sessionBlocks.Add(res.resultText);

            string Text = "";
            for (int i = 0; i < Player.sessionGuesses.Count; i++)
            {
                Text += Player.sessionGuesses[i] + "\r\n" + Player.sessionBlocks[i] + "\r\n";
            }
            await msg.Channel.SendMessageAsync(Text);

            if (res.isSolved)
            {
                Player.isSolved = res.isSolved;
                Player.currStreak++;
                Player.lastSolved = Game.WordleNum;
                if (Player.highStreak < Player.currStreak) { Player.highStreak = Player.currStreak; }
                Player.SolveCounter[Player.sessionGuesses.Count - 1]++;
                await msg.Channel.SendMessageAsync($"Congrats!! you got the word\r\ntime untill next wordle is {GetNextTime()}");
                Text = "Wordle " + (Game.WordleNum + 1) + " " + Player.sessionGuesses.Count + "/6\r\n";
                for (int i = 0; i < Player.sessionGuesses.Count; i++)
                {
                    Text += Player.sessionBlocks[i] + "\r\n";
                }
                await msg.Channel.SendMessageAsync(Text);
                return;
            }

            if (Player.sessionGuesses.Count == 6)
            {
                Player.currStreak = 0;
                await msg.Channel.SendMessageAsync($"you have run out of guesses!\r\nthe word was -> ||{Game.wordlist[Game.WordleNum]}||\r\ntime untill next wordle is {GetNextTime()}");
                Text = "Wordle " + (Game.WordleNum + 1) + "X/6\r\n";
                for (int i = 0; i < Player.sessionGuesses.Count; i++)
                {
                    Text += Player.sessionBlocks[i] + "\r\n";
                }
                await msg.Channel.SendMessageAsync(Text);
                return;
            }
        }

        private async Task<bool> doChecks(playerdata player, string word, ISocketMessageChannel channel)
        {
            if (player.isSolved)
            {
                await channel.SendMessageAsync($"you have already solved todays wordle!\r\ntime untill next wordle is {GetNextTime()}");
                return false;
            }
            else if (player.sessionGuesses.Count == 6)
            {
                await channel.SendMessageAsync($"you have already used all your guesses today, try again tomorrow\r\ntime untill next wordle is {GetNextTime()}");
                return false;
            }
            if (Game.validWord(word))
            {
                return true;
            }
            await channel.SendMessageAsync($"\"{word}\" does not exist in the wordlist");
            return false;
        }

        string GetNextTime()
        {
            return wordleCountDown().Hours + " hours " + wordleCountDown().Minutes + " minutes " + wordleCountDown().Seconds + " seconds";
        }

        TimeSpan wordleCountDown()
        {
            return (Game.startDate.AddDays(Game.WordleNum + 1) - DateTime.UtcNow);
        }

        private void updatePlayer(playerdata player)
        {
            //Check if a streak has been broken
            if (player.lastSolved != Game.WordleNum)
            {

                if (player.lastSolved + 1 != Game.WordleNum)
                {
                    if (player.highStreak < player.currStreak) { player.highStreak = player.currStreak; }
                    player.currStreak = 0;
                }
            }

            if (player.currentSession != Game.WordleNum)
            {
                player.currentSession = Game.WordleNum;
                player.sessionBlocks.Clear();
                player.sessionGuesses.Clear();
                player.isSolved = false;
            }
        }

        private async Task stopBot(ISocketMessageChannel channel)
        {
            foreach (ulong id in activeIDs)
            {
                activePlayers.TryGetValue(id, out playerdata player);
                savePData(player);
            }
            await channel.SendMessageAsync("goodbye <:salute2:976159106899542067>");
            await _client.StopAsync();
            Thread.Sleep(2000);
            Environment.Exit(0);
        }

        private bool isOwner(ulong id) { return OwnerIDs.Contains(id); }
        private async Task wrongPrivelege(ISocketMessageChannel channel)
        {
            await channel.SendMessageAsync("Only the owners of the bot can do that");
        }

        public playerdata GetPlayer(ulong ID)
        {
            playerdata Player;
            if (!activePlayers.ContainsKey(ID))
            {
                if (!File.Exists($"data\\userdata\\{ID}.json"))
                {
                    Player = new playerdata()
                    {
                        PlayerID = ID,
                        currentSession = Game.WordleNum,
                        LastAction = DateTime.UtcNow
                    };
                }
                else
                {
                    Player = JsonConvert.DeserializeObject<playerdata>(File.ReadAllText($"data\\userdata\\{ID}.json"));
                }
                activePlayers.Add(Player.PlayerID, Player);
                activeIDs.Add(Player.PlayerID);
            }
            else
            {
                activePlayers.TryGetValue(ID, out Player);
            }
            return Player;
        }

        public async Task DisplayStats(SocketUser user, playerdata Player, ISocketMessageChannel channel)
        {
            EmbedBuilder stats = new EmbedBuilder();
            stats.Color = Color.Purple;
            stats.Title = "Stats - " + user.Username;
            stats.AddField(CreateField("Streak: ", $"Max streak: {Player.highStreak}\r\nCurrent streak: {Player.currStreak}"));
            int highestSolved = Player.SolveCounter.Max();

            string solves = "";
            for (int i = 0; i < Player.SolveCounter.Length; i++)
            {
                solves += (i + 1) + " tries] ";
                for (int j = 0; j < (int)((float)Player.SolveCounter[i] / (float)highestSolved / 2f * 10f); j++)
                {
                    solves += Game.GreenSquare;
                }
                solves += " " + Player.SolveCounter[i] + "\r\n";
            }

            stats.AddField(CreateField("Solves:", solves));
            await channel.SendMessageAsync("", false, stats.Build());
        }
        EmbedFieldBuilder CreateField(string Name, string Value = "-", bool inLine = false)
        {
            return new EmbedFieldBuilder() { Name = Name, Value = Value, IsInline = inLine };
        }
        private async Task HelpPage(ISocketMessageChannel channel, ulong id)
        {
            EmbedBuilder page = new EmbedBuilder();
            page.Color = Color.Red;
            page.Title = "Commands - prefix '?'";
            if (isOwner(id))
            {
                page.AddField("quit", "stops the bot and saves currently loaded data");
                page.AddField("rem <User ID>", "removes users data");
                page.AddField("word", "displays the word of the day");
            }
            page.AddField("rules", "displays a \"how to play\" guide");
            page.AddField("g <WORD>", "makes a guess on the current wordle");
            page.AddField("stats", "displays your current statistics");
            page.AddField("help", "displays this page");
            page.AddField("result", "displays the end result of your last wordle");
            await channel.SendMessageAsync("", false, page.Build());
        }
    }
}

