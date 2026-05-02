namespace UltimaAnimationForge.Models;

public enum CompressionFlag
{
    None = 0,
    Zlib = 1,
    Mythic = 3
}

public class UopAnimationIndexEntry
{
    public int Lookup { get; set; } = -1;
    public int Length { get; set; } = -1;
    public int DecompressedLength { get; set; } = -1;
    public CompressionFlag Flag { get; set; } = CompressionFlag.None;
    public int Extra { get; set; } = -1;
    public int Extra1 { get; set; } = -1;
    public int Extra2 { get; set; } = -1;

    public bool IsValid => Lookup >= 0 && Length > 0;
}

public sealed class UopBodySlotEntry
{
    public int BodyId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string BodyType { get; set; } = string.Empty;

    public int ActionCount { get; set; }

    public string DisplayText => FileName + " | Body " + BodyId;

    public string SecondaryText => BodyType + " | " + ActionCount + " actions";
}

public struct UopDataHeader(ulong offset, uint headerSize, uint compressedSize, uint decompressedSize, ulong hash, ushort flag)
{
    public ulong Offset { get; set; } = offset;

    public uint HeaderSize { get; set; } = headerSize;

    public uint CompressedSize { get; set; } = compressedSize;

    public uint DecompressedSize { get; set; } = decompressedSize;

    public ulong Hash { get; set; } = hash;

    public ushort Flag { get; set; } = flag;
}

public sealed class UopFileData
{
    public ulong Hash { get; set; }

    public byte[] Data { get; set; } = [];

    public byte[]? PrecompressedData { get; set; }

    public byte[]? HeaderBytes { get; set; }

    public uint HeaderSize { get; set; }

    public uint DecompressedSize { get; set; }

    public bool IsCompressed { get; set; } = true;

    public bool IsEmpty { get; set; }
}

public class UopTableEntry
{
    public ulong Offset { get; set; }
    public uint HeaderLength { get; set; }
    public uint CompressedLength { get; set; }
    public uint DecompressedLength { get; set; }
    public ulong Identifier { get; set; }
    public uint DataBlockHash { get; set; }
    public short Compression { get; set; }

    public bool IsValid => Identifier != 0 && DecompressedLength != 0 && Offset != 0;
}

public static class UopConstants
{
    public static class ActionNames
    {
        public static readonly string[] MonsterActions =
        {
            "Walk",
            "Idle",
            "Die1",
            "Die2",
            "Attack1",
            "Attack2",
            "Attack3",
            "AttackBow",
            "AttackCrossBow",
            "AttackThrow",
            "GetHit",
            "Pillage",
            "Stomp",
            "Cast2",
            "Cast3",
            "BlockRight",
            "BlockLeft",
            "Idle",
            "Fidget",
            "Fly",
            "TakeOff",
            "GetHitInAir"
        };

        public static readonly string[] AnimalActions =
        {
            "Walk",
            "Run",
            "Idle",
            "Eat",
            "Alert",
            "Attack1",
            "Attack2",
            "GetHit",
            "Die1",
            "Idle",
            "Fidget",
            "LieDown",
            "Die2"
        };

        public static readonly string[] HumanActions =
        {
            "Walk_01",
            "WalkStaff_01",
            "Run_01",
            "RunStaff_01",
            "Idle_01",
            "Idle_01",
            "Fidget_Yawn_Stretch_01",
            "CombatIdle1H_01",
            "CombatIdle1H_01",
            "AttackSlash1H_01",
            "AttackPierce1H_01",
            "AttackBash1H_01",
            "AttackBash2H_01",
            "AttackSlash2H_01",
            "AttackPierce2H_01",
            "CombatAdvance_1H_01",
            "Spell1",
            "Spell2",
            "AttackBow_01",
            "AttackCrossbow_01",
            "GetHit_Fr_Hi_01",
            "Die_Hard_Fwd_01",
            "Die_Hard_Back_01",
            "Horse_Walk_01",
            "Horse_Run_01",
            "Horse_Idle_01",
            "Horse_Attack1H_SlashRight_01",
            "Horse_AttackBow_01",
            "Horse_AttackCrossbow_01",
            "Horse_Attack2H_SlashRight_01",
            "Block_Shield_Hard_01",
            "Punch_Punch_Jab_01",
            "Bow_Lesser_01",
            "Salute_Armed1h_01",
            "Ingest_Eat_01"
        };

        public static readonly string[] CharActions =
        {
            "Walk",
            "Walk (With Weapon)",
            "Run",
            "Run (With Weapon)",
            "Idle",
            "Idle (With Weapon)",
            "Fidget",
            "Idle - Combat (1H Weapon)",
            "Idle - Combat (2H Weapon)",
            "Slash Attack (1H Weapon)",
            "Pierce Attack (1H Weapon)",
            "Bash Attack (1H Weapon)",
            "Bash Attack (2H Weapon)",
            "Slash Attack (2H Weapon)",
            "Pierce Attack (2H Weapon)",
            "Combat Walk (2H Weapon)",
            "Spell 1",
            "Spell 2",
            "Bow Attack",
            "Crossbow Attack",
            "Get Hit",
            "Die Backward",
            "Die Forward",
            "Walk Mounted",
            "Run Mounted",
            "Idle Mounted",
            "Bash Attack Mounted",
            "Bow Attack Mounted",
            "Crossbow Attack Mounted",
            "Slash Attack Mounted",
            "Shield Block",
            "Punch",
            "Bowing",
            "Salute (Armed)",
            "Drinking",
            "Combat Walk (1H Weapon)",
            "Combat Walk (Unarmed)",
            "Idle (Shield)",
            "Sitting",
            "Get Hit (2H Weapon)",
            "Mining",
            "Idle - Combat (Shield)",
            "Drinking (Sat Down)",
            "",
            "",
            "",
            "",
            "Idle (2H Weapon) Mounted",
            "Get Hit Mounted",
            "Spell Cast Mounted",
            "Get Hit (Shield) Mounted",
            "Drinking Mounted",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "Take off",
            "Land",
            "Fly Forward (Slow)",
            "Fly Forward (Fast)",
            "Fly Idle",
            "Fly Idle Combat",
            "Fly Fidget",
            "Fly Fidget 2",
            "Fly Get Hit",
            "Fly Die Backward",
            "Fly Die Forward",
            "Fly Attack (1H Weapon)",
            "Fly Attack (2H Weapon)",
            "Fly Attack (Boomerang)",
            "Fly Get Hit (Shield)",
            "Fly Spell 1",
            "Fly Spell 2",
            "Fly Get Hit",
            "Fly Drinking"
        };
    }
}
