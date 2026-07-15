namespace RandomMagicConversion;

public sealed class GenerationRequest
{
    public string ProjectDir { get; init; }
    public string InputRegulationPath { get; init; }
    public string OutputRegulationPath { get; init; }
    public string ShopConfigPath { get; init; }
    public string MapLootConfigPath { get; init; }
    public string BossRewardConfigPath { get; init; }
    public string EnemyLotConfigPath { get; init; }
    public string FarmableEnemyLootSuppressionConfigPath { get; init; }
    public string AshOfWarConfigPath { get; init; }
    public string StartingClassConfigPath { get; init; }
    public string BaseItemMsgbndOverridePath { get; init; }
    public string Dlc2ItemMsgbndOverridePath { get; init; }
    public int? SeedOverride { get; init; }
    public bool EnableShieldConversion { get; init; } = true;
    public bool EnableShopConversion { get; init; } = true;
    public bool EnableMapLootConversion { get; init; } = true;
    public bool EnableBossRewardConversion { get; init; } = true;
    public bool EnableEnemyLotConversion { get; init; } = true;
    public bool EnableFarmableEnemyLootSuppression { get; init; } = true;
    public bool EnableAshOfWarConversion { get; init; } = true;
    public bool EnableStartingClassConversion { get; init; } = true;
    public bool GenerateTextOutputs { get; init; } = true;
    public bool UseRandomizerFriendlyShieldUpgradePath { get; init; } = true;
    public bool RemoveStandaloneStatRequirements { get; init; }
    public string SpellPoolModeOverride { get; init; }
}

public sealed class GenerationResult
{
    public int Seed { get; init; }
    public string OutputRegulationPath { get; init; } = string.Empty;
    public string ShieldMappingPath { get; init; } = string.Empty;
    public string ShopMappingPath { get; init; } = string.Empty;
    public string MapLootMappingPath { get; init; } = string.Empty;
    public string BossRewardMappingPath { get; init; } = string.Empty;
    public string EnemyLotMappingPath { get; init; } = string.Empty;
    public string FarmableEnemyLootSuppressionMappingPath { get; init; } = string.Empty;
    public string AshOfWarMappingPath { get; init; } = string.Empty;
    public string StartingClassMappingPath { get; init; } = string.Empty;
    public int ShieldMappingCount { get; init; }
    public int StartingClassReplacementCount { get; init; }
    public int ShopReplacementCount { get; init; }
    public int MapLootReplacementCount { get; init; }
    public int BossRewardReplacementCount { get; init; }
    public int EnemyLotReplacementCount { get; init; }
    public int FarmableEnemyLootSuppressionRowCount { get; init; }
    public int AshOfWarReplacementCount { get; init; }
}
