﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Users;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public class PlaySongSelect : SongSelect
    {
        private bool removeAutoModOnResume;
        private Mod removableMod;
        private Mod manualMod;

        private OsuScreen player;

        [Resolved(CanBeNull = true)]
        private NotificationOverlay notifications { get; set; }

        public override bool AllowExternalScreenChange => true;

        protected override UserActivity InitialActivity => new UserActivity.ChoosingBeatmap();

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            BeatmapOptions.AddButton(@"Edit", @"beatmap", FontAwesome.Solid.PencilAlt, colours.Yellow, () => Edit());

            ((PlayBeatmapDetailArea)BeatmapDetails).Leaderboard.ScoreSelected += PresentScore;
        }

        protected void PresentScore(ScoreInfo score) =>
            FinaliseSelection(score.Beatmap, score.Ruleset, () => this.Push(new SoloResultsScreen(score, false)));

        protected override BeatmapDetailArea CreateBeatmapDetailArea() => new PlayBeatmapDetailArea();

        public override void OnResuming(IScreen last)
        {
            base.OnResuming(last);

            player = null;

            if (removeAutoModOnResume)
            {
                var modType = removableMod?.GetType();

                if (modType != null)
                    ModSelect.DeselectTypes(new[] { modType }, true);

                if (manualMod != null)
                {
                    Mods.Value = Mods.Value.Append(manualMod).ToArray();
                    manualMod = null;
                }

                removableMod = null;
                removeAutoModOnResume = false;
            }
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                case Key.KeypadEnter:
                    // this is a special hard-coded case; we can't rely on OnPressed (of SongSelect) as GlobalActionContainer is
                    // matching with exact modifier consideration (so Ctrl+Enter would be ignored).
                    FinaliseSelection();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        protected override bool OnStart()
        {
            if (player != null) return false;

            // Ctrl+Enter should start map with autoplay enabled.
            if (GetContainingInputManager().CurrentState?.Keyboard.ControlPressed == true)
            {
                var cinema = GetContainingInputManager().CurrentState?.Keyboard.ShiftPressed;
                var mod = (cinema == true) ? Ruleset.Value.CreateInstance().GetCinemaMod() : Ruleset.Value.CreateInstance().GetAutoplayMod();
                var placeholderText = (cinema == true) ? "a cinema" : "an autoplay";
                var modType = mod?.GetType();

                if (modType == null)
                {
                    notifications?.Post(new SimpleNotification
                    {
                        Text = $"The current ruleset doesn't have {placeholderText} mod avalaible!"
                    });
                    return false;
                }

                manualMod = getActiveAutoMod();
                var manualModType = manualMod?.GetType();

                if (manualModType != null && manualModType != modType)
                {
                    ModSelect.DeselectTypes(new[] { manualModType }, true);
                }

                var mods = Mods.Value;

                if (mods.All(m => m.GetType() != modType))
                {
                    Mods.Value = mods.Append(mod).ToArray();
                    removableMod = mod;
                    removeAutoModOnResume = true;
                }
            }

            SampleConfirm?.Play();

            this.Push(player = new PlayerLoader(() => new Player()));

            return true;
        }

        private Mod getActiveAutoMod()
        {
            if (Mods.Value.Any(m => m.GetType() == Ruleset.Value.CreateInstance().GetAutoplayMod()?.GetType())) return Ruleset.Value.CreateInstance().GetAutoplayMod();
            else if (Mods.Value.Any(m => m.GetType() == Ruleset.Value.CreateInstance().GetCinemaMod()?.GetType())) return Ruleset.Value.CreateInstance().GetCinemaMod();

            return null;
        }
    }
}
