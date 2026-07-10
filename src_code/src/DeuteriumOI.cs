using System;
using System.Linq;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace Deuterium
{
    internal sealed class DeuteriumOI : OptionInterface
    {
        public const string ModId = "brokenbyteofcode.ukrlocale";

        private static readonly string[] EasterEggPhrases =
        {
            "You found the easter egg. Bloop.",
            "I paid 25 cents for this easter egg",
            "Why you even read that? It's just a bunch of text.",
            "I don't even know what to say anymore."
        };

        private OpLabel? _easterEggLabel;
        private int _phraseIndex = -1;

        public override void Initialize()
        {
            base.Initialize();
            Tabs = new OpTab[1]
            {
                new OpTab(this, OptionInterface.Translate("About"))
            };

            OpSimpleButton button = new OpSimpleButton(new Vector2(225f, 290f), new Vector2(150f, 30f), OptionInterface.Translate("Press me"))
            {
                description = OptionInterface.Translate("Press at your own risk.")
            };
            button.OnClick += EasterEggButton_OnClick;
            Tabs[0].AddItems(button);

            _easterEggLabel = new OpLabel(new Vector2(75f, 220f), new Vector2(450f, 50f), string.Empty, FLabelAlignment.Center)
            {
                autoWrap = true,
                color = MenuColorEffect.rgbWhite
            };
            Tabs[0].AddItems(_easterEggLabel);
        }

        private void EasterEggButton_OnClick(UIfocusable trigger)
        {
            if (_easterEggLabel == null)
            {
                return;
            }

            _phraseIndex = (_phraseIndex + 1) % EasterEggPhrases.Length;
            _easterEggLabel.text = EasterEggPhrases[_phraseIndex];
        }

        internal static void Register()
        {
            try
            {
                MachineConnector.SetRegisteredOI(ModId, new DeuteriumOI());
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Deuterium] Failed to register Remix OptionInterface: " + ex.Message);
            }
        }
    }
}
