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

    /*
     * MiraAPI's CreateButton() names each ability's GameObject
     * "{Name}Button" (see TargetedMeetingButton.CreateButton), and
     * attaches a MeetingAbilityBehaviour component to it - not this
     * class. RoleJotButton itself is a plain C# class, never a
     * Component, so GetComponent<RoleJotButton>() can never match
     * anything. Identify "is this the Jot button" by that stable
     * GameObject name instead.
     */
    private static readonly string JotButtonGameObjectName = new RoleJotButton().Name + "Button";

    [RegisterEvent]
    public static void OnMeetingSelect(MeetingSelectEvent @event)
    {
        if (@event.VoteData.VotesRemaining > 0) return;

        @event.AllowSelect = true;
        var submitButton = typeof(MeetingHud)
            .GetField("m_SubmitButton", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(MeetingHud.Instance) as Component;
        submitButton?.gameObject.SetActive(false);

        HideNonJotButtons();
    }

    [RegisterEvent]
    public static void OnHandleVote(HandleVoteEvent @event)
    {
        if (@event.VoteData.VotesRemaining <= 0) @event.Cancel();
    }

    /*
     * Once votes are spent, only Jot should still be usable -
     * everywhere, not just on whichever row was last clicked. Every
     * targeted ability button (Jot, TOU's Prosecute, etc.) gets
     * parented under every player's playerVoteArea.Buttons by
     * MiraAPI's MeetingButtonManager, so we sweep every row (plus
     * the Skip button, which uses the same PlayerVoteArea UI).
     */
    private static void HideNonJotButtons()
    {
        if (MeetingHud.Instance == null) return;

        foreach (var playerVoteArea in MeetingHud.Instance.playerStates)
        {
            if (playerVoteArea != null) HideConfirmButton(playerVoteArea);
        }

        if (MeetingHud.Instance.SkipVoteButton != null) HideConfirmButton(MeetingHud.Instance.SkipVoteButton);
    }

    private static void HideConfirmButton(PlayerVoteArea playerVoteArea)
    {
        foreach (var button in playerVoteArea.Buttons.GetComponentsInChildren<PassiveButton>(true))
        {
            if (button == playerVoteArea.CancelButton) continue;
            if (button.gameObject.name == JotButtonGameObjectName) continue;

            button.gameObject.SetActive(false);
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
