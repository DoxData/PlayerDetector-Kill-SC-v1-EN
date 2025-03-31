using Newtonsoft.Json; // Añadir directiva using para Newtonsoft.Json
using System.IO;
using System.Media;

namespace PlayerDetector-Kill-SC-v1-EN
{
    public class AlertSystem
    {
        private dynamic config;
        private DateTime lastRAMAlert;

        public AlertSystem()
        {
            config = JsonConvert.DeserializeObject(File.ReadAllText("alerts.json"))!;
        }

        public void CheckForAlerts()
        {
            var ramUsage = GetCurrentRAMUsage();
            if (ramUsage > config.RAMAlert.Threshold &&
                (DateTime.Now - lastRAMAlert).TotalMilliseconds > config.RAMAlert.Cooldown)
            {
                PlayAlertSound();
                lastRAMAlert = DateTime.Now;
            }
        }

        public void CheckForDeathAlerts(DeathEvent death)
        {
            if (config.PVPDeathAlert.Enabled && death.LogLine.Contains("killed by"))
            {
                PlayAlertSound(config.PVPDeathAlert.Sound);
            }
        }

        private int GetCurrentRAMUsage()
        {
            // Implement logic to get current RAM usage
            return 0;
        }

        private void PlayAlertSound()
        {
            // Implement logic to play alert sound
        }

        private void PlayAlertSound(string soundFile)
        {
            using (var soundPlayer = new SoundPlayer(soundFile))
            {
                soundPlayer.Play();
            }
        }
    }
}
// Copyright (c) 2024 DoxData. Exclusive ownership. 
// Prohibited use/modification without express authorization.
