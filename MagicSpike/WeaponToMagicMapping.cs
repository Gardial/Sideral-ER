namespace RandomMagicConversion;

public sealed class WeaponToMagicMapping
{
    public int TargetWeaponId { get; set; }
    public int SourceMagicId { get; set; }
    public int SourceGoodsId { get; set; }

    public bool IsSorcery { get; set; }

    public override string ToString()
    {
        string type = IsSorcery ? "Sorcery" : "Incantation";
        return $"{TargetWeaponId} => Magic {SourceMagicId} / Goods {SourceGoodsId} ({type})";
    }
}