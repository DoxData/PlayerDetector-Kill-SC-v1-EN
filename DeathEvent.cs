namespace PlayerDetector-Kill-SC-v1-EN
{
    // ...existing code...
    public class DeathEvent
    {
        public string LogLine { get; }
        public DateTime Timestamp { get; }
        public string VictimName { get; }
        public string VictimId { get; }
        public string Zone { get; }
        public string KillerName { get; }
        public string KillerId { get; }
        public string WeaponName { get; }
        public string WeaponClass { get; }
        public string WeaponId { get; }
        public string DamageType { get; }
        public string DirX { get; }
        public string DirY { get; }
        public string DirZ { get; }
        public string Team { get; }

        public DeathEvent(string logLine, string timestamp, string victimName, string victimId, string zone, string killerName, string killerId, string weaponName, string weaponClass, string weaponId, string damageType, string dirX, string dirY, string dirZ, string team)
        {
            LogLine = logLine;
            Timestamp = DateTime.Parse(timestamp);
            VictimName = victimName;
            VictimId = victimId;
            Zone = zone;
            KillerName = killerName;
            KillerId = killerId;
            WeaponName = weaponName;
            WeaponClass = weaponClass;
            WeaponId = weaponId ?? "Unknown"; // Ensure WeaponId is not null
            DamageType = damageType;
            DirX = dirX;
            DirY = dirY;
            DirZ = dirZ;
            Team = team;
        }

        public override string ToString()
        {
            return DamageType switch
            {
                "suicide" => "DeathSuicide",
                "bullet" when KillerName != "NPC" => "DeathPlayer",
                "bullet" when KillerName == "NPC" => "DeathNPC",
                "crash" => "DeathCrash",
                _ => "DeathEvent"
            };
        }
// Copyright (c) 2024 DoxData. Exclusive ownership. 
// Prohibited use/modification without express authorization.
    }
}
