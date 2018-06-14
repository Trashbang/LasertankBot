using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using TweetSharp;

namespace LaserTank
{
    class Program
    {
        enum Difficulty { None = 0, Kids = 1, Easy = 2, Medium = 4, Hard = 8, Deadly = 16 }; // I don't know why, I guess our developer friend thought setting individual bits was cool
        const int MAX_TWEET_LENGTH = 280;
        struct Level
        {
            public Difficulty difficulty;
            public string levelName;
            public string authorName;
            public string hint;
            public byte[,] levelMap;

            public Level(Difficulty difficultyIn, string levelNameIn, string authorNameIn, string hintIn, byte[,] levelMapIn)
            {
                difficulty = difficultyIn;
                levelName = levelNameIn;
                authorName = authorNameIn;
                hint = hintIn;
                levelMap = levelMapIn;
            }
        }

        static void Main(string[] args)
        {
            Random rng = new Random();
            if (args.Length >= 1)
            {
                List<Level> levels = new List<Level>();
                string laserTankDirectory = args[0];
                string[] filePaths = Directory.GetFiles(laserTankDirectory);
                foreach (string filePath in filePaths)
                {
                    if (Path.GetExtension(filePath).Equals(".lvl"))
                    {
                        levels.AddRange(getLvlContents(filePath));
                    }
                }
                int levelSelection = rng.Next(levels.Count);
                Level level = levels[levelSelection];
                string tweetBody = constructTweet(level);
                Console.Write(tweetBody);
                Bitmap levelBitmap = createBitmap(level.levelMap);
                levelBitmap.Save("output.png", System.Drawing.Imaging.ImageFormat.Png);
                postTweet(tweetBody, "output.png");
                //Console.ReadKey();
            }
        }

        static void postTweet(string tweetBody, string imagePath)
        {
            try
            {
                Stream image = new FileStream("output.png", FileMode.Open);
                
                // This wrapper is weird so we've got to put our image in a Dictionary first 
                // (but allegedly the key doesn't do anything?)
                // I'm just going to put the filename in there, so it looks like I know what I'm doing
                Dictionary<string, Stream> imageHolder = new Dictionary<string, Stream>();
                imageHolder.Add("output.png", image);

                // TwitterService doesn't implement IDisposable
                // Uh, I don't *think* that's a problem??
                TwitterService service = new TwitterService(Auth.AUTH_CONSUMER_KEY, Auth.AUTH_CONSUMER_SECRET);
                service.AuthenticateWith(Auth.AUTH_ACCESS_TOKEN, Auth.AUTH_ACCESS_TOKEN_SECRET);

                // siri_send_tweet()
                SendTweetWithMediaOptions tweetOptions = new SendTweetWithMediaOptions();
                tweetOptions.Status = tweetBody;
                tweetOptions.Images = imageHolder;
                service.SendTweetWithMedia(tweetOptions);
                log("TWEETED: " + tweetBody);
            }
            catch (Exception e) // What kind of exception does TweetSharp throw and why can I not figure this out?
            {
                Console.WriteLine(e.Message);
                log(e.Message);
                // I just don't know, man
            }
        }

        // Makes the text for the tweet body based on the level you pass to it. Basically just formatting a bunch of fields together.
        static string constructTweet(Level level)
        {
            string tweetBody = "Name: " + level.levelName + "\nBy: " + level.authorName;
            if (level.difficulty != 0) // Only add difficulty if it's nonzero (zero = the designer didn't set it)
            {
                tweetBody += "\nDifficulty: " + level.difficulty.ToString();
            }
            if ((!string.IsNullOrWhiteSpace(level.hint)) && (tweetBody.Length + level.hint.Length + 2 <= MAX_TWEET_LENGTH)) // Don't add the hint if it's blank, empty, or if it'd overflow the character limit
            {
                tweetBody += ("\n\nHint: \"" + level.hint + "\"");
            }
            return tweetBody;
        }

        static void log(string message)
        {
            string dateTime = DateTime.Now.ToString(); // there are bunch of overrides for this ofc but the basic option should do
            using (StreamWriter logFile = new StreamWriter("LaserTank.log", true)) // boolean is "do you want to append?", obviously we do
            {
                logFile.WriteLine("================================================================");
                logFile.WriteLine(dateTime);
                logFile.WriteLine("================================================================");
                logFile.WriteLine();
                logFile.Write(message);
                logFile.WriteLine();
                logFile.WriteLine("================================================================");
            }
        }

        // Copies a patch of size (width, height) from source[x1, y1] to dest[x2, y2]. 
        // You'd think this would be in the Bitmap class already, right?
        static void copyPatch(Bitmap source, Bitmap dest, int x1, int y1, int x2, int y2, int width, int height)
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Color currentPixel = source.GetPixel(x1 + i, y1 + j);
                    dest.SetPixel(x2 + i, y2 + j, currentPixel);
                }
            }
        }

        // Takes the abstract representation of the level and constructs the actual visual representation you'd normally see
        static Bitmap createBitmap(byte[,] levelMap)
        {
            // Each tile is 32x32. The key for the map is as follows:
            // 0 = Ground
            // 1 = Tank
            // 2 = Flag
            // 3 = Water
            // 4 = Stone block
            // 5 = Pushable block
            // 6 = Bricks
            // 7 = Antitank (up)
            // 8 = Antitank (right)
            // 9 = Antitank (down)
            // 10 = Antitank (left)
            // 11 = Mirror (up-left)
            // 12 = Mirror (up-right)
            // 13 = Mirror (down-right)
            // 14 = Mirror (down-left)
            // 15 = Conveyor (up)
            // 16 = Conveyor (right)
            // 17 = Conveyor (down)
            // 18 = Conveyor (left)
            // 19 = Glass block
            // 20 = Movable mirror (up-left)
            // 21 = Movable mirror (up-right)
            // 22 = Movable mirror (down-right)
            // 23 = Movable mirror (down-left)
            // 24 = Ice
            // 25 = Cracked ice
            // 64 = Tunnel (0, red)
            // 66 = Tunnel (1, green)
            // 68 = Tunnel (2, blue)
            // 70 = Tunnel (3, cyan)
            // 72 = Tunnel (4, yellow)
            // 74 = Tunnel (5, pink)
            // 76 = Tunnel (6, white)
            // 78 = Tunnel (7, black)
            Bitmap bitmap = new Bitmap(32*16, 32*16, System.Drawing.Imaging.PixelFormat.Format32bppArgb); 
            try
            {
                using (Bitmap tiles = new Bitmap(System.Environment.CurrentDirectory + "\\tiles.bmp"))
                {
                    for (int i = 0; i < 16; i++)
                    {
                        for (int j = 0; j < 16; j++)
                        {
                            // Copy out the appropriate tile from the tiles bitmap into the space at (j*32, i*32)
                            byte tileType = levelMap[i, j];
                            if (tileType >= 64) // Bring these into alignment so they follow naturally from the rest of the tiles (see tile list)
                            {
                                tileType = (byte)((int)tileType / 2 - 6);
                            }
                            copyPatch(tiles, bitmap, tileType * 32, 0, j * 32, i * 32, 32, 32);
                        }
                    }
                    // Give the bitmap a nice old-school frame with the coordinate numbers/letters around the edges
                    using (Bitmap frame = new Bitmap(System.Environment.CurrentDirectory + "\\frame.bmp")) 
                    {
                        copyPatch(bitmap, frame, 0, 0, 17, 17, 512, 512);
                        bitmap = new Bitmap(frame);
                    }
                }
                // Pixel art hack to stop Twitter compressing it into a horrid jpeg
                bitmap.SetPixel(0, 0, Color.Empty);
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Could not find file.");
                Console.WriteLine(e.Message);
                log(e.Message);
            }
            catch (FileLoadException e)
            {
                Console.WriteLine("File was found, but could not be loaded.");
                Console.WriteLine(e.Message);
                log(e.Message);
            }

            return bitmap; 
        }

        // Cracks open the .lvl file located at the given path and constructs a list of every level in it.
        // I don't know what happens if you pass a bad file, probably just an empty list
        static List<Level> getLvlContents(string filePath)
        //Format:
        // 256 chars (map)
        // Level name(plaintext)
        // some number of NULLs(arbitrary??)
        // Hint contents(plaintext, can contain \n)
        // more NULLs
        // Author name(plaintext)
        // more NULLs
        // OPTIONAL: Difficulty level(SOH = Kids, STX = Easy, EOT = Medium, BS = Hard, DLE = Deadly)
        // NULL
        {
            List<Level> levels = new List<Level>();
            try
            {
                using (BinaryReader lvlFile = new BinaryReader(File.Open(filePath, FileMode.Open)))
                {
                    byte[,] levelMap = new byte[16, 16];
                    byte nextByte;
                    string levelName;
                    string hintContents;
                    string authorName;
                    Difficulty difficulty;
                    while (lvlFile.PeekChar() != -1) // -1 means EOF (or can't be read lol)
                    {
                        // The first 256 bytes are the map, so we just grab those in a nested loop and organise into 16x16 array
                        for (int i = 0; i < 16; i++) 
                        {
                            for (int j = 0; j < 16; j++)
                            {
                                nextByte = lvlFile.ReadByte();
                                levelMap[j, i] = nextByte;
                            }
                        }
                        // The level name can occupy UP TO the next 29 bytes, followed by two NULL bytes. 
                        // If we hit a NULL early, we ignore everything between that and the 31-byte limit,
                        // since LaserTank is lazy and doesn't overwrite those bytes 
                        levelName = "";
                        bool hitNull = false;
                        for (int i = 0; i < 31; i++) // We need to advance the pointer by this many bytes regardless of what happens, so we might as well use a for loop
                        {
                            nextByte = lvlFile.ReadByte();
                            if (nextByte == 0)
                            {
                                hitNull = true;
                            }
                            if (!hitNull)
                            {
                                levelName += (char)nextByte;
                            }
                        }
                        // Hint contents are similar to level names, except they occupy UP TO the next 180 bytes,
                        // and are followed by 76 NULL bytes to make a nice neat 256-byte block (except it's not
                        // nicely aligned, but whatever).
                        // Once again, encountering an early NULL means ignoring everything that follows it.
                        hintContents = "";
                        hitNull = false;
                        for (int i = 0; i < 256; i++)
                        {
                            nextByte = lvlFile.ReadByte();
                            if (nextByte == 0)
                            {
                                hitNull = true;
                            }
                            if (!hitNull)
                            {
                                hintContents += (char)nextByte;
                            }
                        }
                        // Similar story again with author name. Up to 29 bytes, followed by two NULL bytes.
                        // Ignore following bytes if we get an early null.
                        authorName = "";
                        hitNull = false;
                        for (int i = 0; i < 31; i++) 
                        {
                            nextByte = lvlFile.ReadByte();
                            if (nextByte == 0)
                            {
                                hitNull = true;
                            }
                            if (!hitNull)
                            {
                                authorName += (char)nextByte;
                            }
                        }
                        // The next byte is the difficulty setting.
                        // This can be 0 (not set) but it's far more likely to be nonzero,
                        // especially in published levels
                        difficulty = (Difficulty)lvlFile.ReadByte();
                        // Also one more empty byte, for reasons
                        lvlFile.ReadByte();
                        
                        Level level = new Level(difficulty, levelName, authorName, hintContents, (byte[,])levelMap.Clone());
                        levels.Add(level);
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Could not find file.");
                Console.WriteLine(e.Message);
            }
            catch (FileLoadException e)
            {
                Console.WriteLine("File was found, but could not be loaded.");
                Console.WriteLine(e.Message);
            }
            return levels;
        }
    }
}
