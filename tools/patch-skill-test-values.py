from pathlib import Path

replacements = {
    Path("Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundation.cs"): [
        ('                null, fifteen(0.01m), new[] { new SkillEffectDescriptor("character.maximum_health", SkillModifierKind.Percentage, 1m) }, null);', '                null, fifteen(1m), new[] { new SkillEffectDescriptor("character.maximum_health", SkillModifierKind.Percentage, 1m) }, null);'),
        ('                null, fifteen(0.01m), new[] { new SkillEffectDescriptor("combat.damage", SkillModifierKind.Percentage, 1m) }, null);', '                null, fifteen(1m), new[] { new SkillEffectDescriptor("combat.damage", SkillModifierKind.Percentage, 1m) }, null);'),
        ('                null, fifteen(0.01m), new[] { new SkillEffectDescriptor("rewards.cash", SkillModifierKind.Percentage, 1m) }, null);', '                null, fifteen(1m), new[] { new SkillEffectDescriptor("rewards.cash", SkillModifierKind.Percentage, 1m) }, null);'),
    ],
    Path("Assets/ShooterMover/UI/Game/PlayerHUD.cs"): [('                * (1d + allocation.RankOf(MaximumHealthSkillId) * 0.01d);', '                * (1d + allocation.RankOf(MaximumHealthSkillId) * 1d);')],
    Path("Assets/ShooterMover/UI/Game/PlayerFire.cs"): [('                + allocation.RankOf(DamageSkillId) * 0.01d;', '                + allocation.RankOf(DamageSkillId) * 1d;')],
    Path("Assets/ShooterMover/UI/Game/RunLoot.cs"): [('                1000 + allocation.RankOf(CashDropSkillId) * 10);', '                1000 + allocation.RankOf(CashDropSkillId) * 1000);')],
}

for path, edits in replacements.items():
    text = path.read_text(encoding="utf-8")
    for old, new in edits:
        if text.count(old) != 1:
            raise RuntimeError(f"Unexpected match count in {path}: {old}")
        text = text.replace(old, new)
    path.write_text(text, encoding="utf-8")
