using MiraAPI.MeetingAbilities;
using MiraAPI.Roles;
using MiraAPI.Utilities.Assets;
using NotePadMod.Assets;
using NotePadMod.Compatibility;
using NotePadMod.Patches;
using NotePadMod.UI;
using UnityEngine;

namespace NotePadMod.MeetingAbilities;

public sealed class RoleJotButton : TargetedMeetingButton
{
    public override string Name => "Jot Role";

    public override int MaxUses => 0;

    public override float Cooldown => 1f;
    public override LoadableAsset<Sprite> Sprite => NotepadAssets.JotButtonSprite;

    public override Color OutlineColor => Color.yellow;

    public override bool Enabled(RoleBehaviour r) => TouIntegration.IsTouPresent;

    public override bool IsTargetValid(PlayerVoteArea playerVoteArea)
    {
        if (!base.IsTargetValid(playerVoteArea)) return false;

        var target = GameData.Instance?.GetPlayerById(playerVoteArea.PlayerId.Value)?.Object;
        if (target == null) return true;

        return !TouIntegration.IsRoleAlreadyKnown(target);
    }

    protected override void OnClick(PlayerVoteArea playerVoteArea)
    {
        if (!JottingIntegration.IsAvailable) return;

        var targetId = playerVoteArea.PlayerId.Value;

        /*
         * Clicking Jot on a player who's already jotted removes
         * the label and restores their panel, instead of opening
         * the picker again to overwrite it.
         */
        if (JotedRoleLabels.TryGetLabel(targetId, out _))
        {
            JottedLabelPatch.RemoveJotedLabel(targetId);
            return;
        }

        JottingIntegration.Open(
            _ => true,
            role =>
            {
                var rawName = role is ICustomRole customRole
                    ? customRole.RoleName
                    : TranslationController.Instance?.GetString(role.StringName) ?? role.Role.ToString();

                var styled = RoleColorizer.Apply(rawName);

                JotedRoleLabels.SetLabel(targetId, $"<size=80%>{styled}</size>");
            });
    }
}
