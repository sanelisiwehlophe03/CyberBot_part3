using System;
using System.IO;
using System.Media;

namespace CyberBot.Utils
{
    public static class AudioPlayer
    {
        public static bool PlayGreeting(string fileName = "greeting.wav")
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, fileName);

                if (!File.Exists(path))
                {
                    return false;
                }

                SoundPlayer player = new SoundPlayer(path);
                player.Play();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}