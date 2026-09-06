using System;
using System.Reflection;
using MiraAPI.Events;
using MiraAPI.MeetingAbilities;
using MiraAPI.Roles;
using MiraAPI.Utilities.Assets;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using NotePadMod.Assets;
using NotePadMod.Compatibility;
using NotePadMod.Patches;
using NotePadMod.UI;
using UnityEngine;

namespace NotePadMod.MeetingAbilities;

public sealed class RoleJotButton : TargetedMeetingButton
{
    public static event Action<byte>? JotRequested;

    [RegisterEvent]
    public static void OnMeetingSelect(MeetingSelectEvent @event)
    {
        if (@event.VoteData.VotesRemaining > 0) return;

        @event.AllowSelect = true;
        var submitButton = typeof(MeetingHud)
            .GetField("m_SubmitButton", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(MeetingHud.Instance) as Component;
        submitButton?.gameObject.SetActive(false);

        HideVanillaVoteButtons();
    }

    private static void HideVanillaVoteButtons()
    {
        if (MeetingHud.Instance?.playerStates == null) return;

        foreach (var playerVoteArea in MeetingHud.Instance.playerStates)
        {
            if (playerVoteArea == null) continue;

            Component? voteButton = null;
            var field = typeof(PlayerVoteArea).GetField(
                "VoteButton",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field?.GetValue(playerVoteArea) is Component fieldButton)
                voteButton = fieldButton;

            voteButton?.gameObject.SetActive(false);

            // Some game versions expose the control only as a child object.
            var childButton = playerVoteArea.transform.Find("VoteButton");
            childButton?.gameObject.SetActive(false);
        }
    }

    public void ForceJot(PlayerVoteArea playerVoteArea)
    {
        JotRequested?.Invoke(playerVoteArea.PlayerId.Value);
        OnClick(playerVoteArea);
    }

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
