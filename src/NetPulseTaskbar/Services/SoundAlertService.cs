using System;
using System.Media;

namespace NetPulseTaskbar.Services
{
    public static class SoundAlertService
    {
        public static void PlayDisconnectAlert()
        {
            try
            {
                SystemSounds.Hand.Play();
            }
            catch
            {
                // Ignore audio hardware error
            }
        }

        public static void PlayReconnectAlert()
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch
            {
                // Ignore audio hardware error
            }
        }

        public static void PlayDataCapAlert()
        {
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch
            {
                // Ignore audio hardware error
            }
        }
    }
}
