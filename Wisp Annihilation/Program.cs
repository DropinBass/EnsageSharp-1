﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Menu.MenuItems;
using Ensage.Common.Objects;
using Ensage.Common.Objects.UtilityObjects;
using SharpDX;

namespace Wisp_Annihilation
{
    internal class Program
    {
        private static bool _loaded;
        private static bool IsEnable => Members.Menu.Item("Enable").GetValue<bool>();
        private static bool IsEnableAutoSaver => Members.Menu.Item("autoSaver.Enable").GetValue<bool>();
        private static bool IsEnableautoTether => Members.Menu.Item("autoTether.Enable").GetValue<bool>();
        private static bool UseOrbWalkker => Members.Menu.Item("OrbWalker.Enable").GetValue<bool>();
        private static bool IsComboAim => Members.Menu.Item("Hero.Hotkey.Aim").GetValue<KeyBind>().Active;
        private static bool IsComboHero => Members.Menu.Item("Hero.Hotkey.Combo").GetValue<KeyBind>().Active;
        private static uint HeroAimHotkey => Members.Menu.Item("Hero.Hotkey.Aim").GetValue<KeyBind>().Key;
        private static uint HeroHotkey => Members.Menu.Item("Hero.Hotkey.Combo").GetValue<KeyBind>().Key;

        private static void Main()
        {
            Members.Menu.AddItem(new MenuItem("Enable", "Enable").SetValue(true));
            var settings = new Menu("Settings", "Settings");

            var autoSaver = new Menu("Auto Relocate", "Auto Relocate",false, "wisp_relocate",true);
            autoSaver.AddItem(new MenuItem("autoSaver.Enable", "Enable").SetValue(false));
            autoSaver.AddItem(new AllyHeroesToggler("autoSaver.EnableFor", "For", new Dictionary<string, bool>()));
            autoSaver.AddItem(
                new MenuItem("autoSaver.Percent", "Min Health For Auto Relocate (%)").SetValue(new Slider(15,0,100)));


            var autoTether = new Menu("Auto Tether", "Auto Tether", false, "wisp_tether", true);
            autoTether.AddItem(new MenuItem("autoTether.Enable", "Enable").SetValue(false));
            autoTether.AddItem(new AllyHeroesToggler("autoTether.EnableFor", "For", new Dictionary<string, bool>()));



            settings.AddItem(new MenuItem("OrbWalker.Enable", "Enable Orbwalking").SetValue(true));
            settings.AddItem(
                new MenuItem("Hero.Hotkey.Aim", "Enable Spirits Aim for selected enemy").SetValue(new KeyBind('Z', KeyBindType.Toggle)));
            settings.AddItem(
                new MenuItem("Hero.Hotkey.Combo", "Do Combo Fore Selected Enemy").SetValue(new KeyBind('G', KeyBindType.Press)));
            settings.AddItem(
                new MenuItem("itemEnable", "Toggle Items:").SetValue(
                    new AbilityToggler(new List<string>() {"item_sheepstick"}.ToDictionary(item => item, item => true))));
            var devolper = new Menu("Developer", "Developer");
            devolper.AddItem(new MenuItem("Dev.Text.enable", "Debug messages").SetValue(false));
            settings.AddSubMenu(autoSaver);
            settings.AddSubMenu(autoTether);
            Members.Menu.AddSubMenu(settings);
            Members.Menu.AddSubMenu(devolper);
            _loaded = false;
            Events.OnLoad += (sender, args) =>
            {
                if (_loaded)
                {
                    return;
                }
                Load();
                _loaded = true;
            };
            if (!_loaded && ObjectManager.LocalHero != null &&
                ObjectManager.LocalHero.ClassID == ClassID.CDOTA_Unit_Hero_Wisp && Game.IsInGame)
            {

                Load();
                _loaded = true;
            }

            Events.OnClose += (sender, args) =>
            {
                if (!_loaded)
                    return;
                Members.Menu.RemoveFromMainMenu();
                Game.OnUpdate -= Game_OnUpdate;
                Game.OnUpdate -= AutoSaver;
                Game.OnUpdate -= AutoTeaser;
                Drawing.OnDraw -= Drawing_OnDraw;
                Entity.OnParticleEffectAdded -= EntityOnOnParticleEffectAdded;
                _loaded = false;
            };
        }

        private static void Load()
        {
            if (ObjectManager.LocalHero.ClassID != ClassID.CDOTA_Unit_Hero_Wisp)
                return;
            if (Members.MyHero == null || !Members.MyHero.IsValid)
            {
                Members.MyHero = ObjectManager.LocalHero;
                Members.MyTeam = ObjectManager.LocalHero.Team;
            }
            _attacker = new Sleeper();
            _updater = new Sleeper();
            _multiSleeper = new MultiSleeper();
            Orbwalking.Load();
            Members.Items = new List<string>();
            Members.Menu.AddToMainMenu();
            Game.OnUpdate += Game_OnUpdate;
            Game.OnUpdate += AutoSaver;
            Game.OnUpdate += AutoTeaser;
            Drawing.OnDraw += Drawing_OnDraw;
            Entity.OnParticleEffectAdded += EntityOnOnParticleEffectAdded;
        }
        private static List<Tracker>_trackList=new List<Tracker>(); 
        private static void EntityOnOnParticleEffectAdded(Entity sender, ParticleEffectAddedEventArgs args)
        {
            if (sender.ClassID!=ClassID.CDOTA_Wisp_Spirit)
                return;
            if (sender.Team!=Members.MyTeam)
                return;
            //Printer.Print($"{args.Name}/{args.ParticleEffect.Name}");
            _trackList.Add(new Tracker(sender,args.ParticleEffect));
        }

        private static void AutoTeaser(EventArgs args)
        {
            if (!IsEnableautoTether)
                return;
            var tether = Abilities.FindAbility("wisp_tether");
            
            if (tether == null || tether.Level == 0 || !tether.CanBeCasted() || tether.IsHidden)
                return;
            
            var anyPossibleAlly =
                Heroes.GetByTeam(Members.MyTeam)
                    .FirstOrDefault(
                        x =>
                            x != null && Helper.IsHeroEnableForTether(x.StoredName()) &&
                            x.Distance2D(Members.MyHero) <= tether.CastRange);
            if (anyPossibleAlly == null)
                return;
            
            if (!_multiSleeper.Sleeping(tether))
            {
                tether.UseAbility(anyPossibleAlly);
                _multiSleeper.Sleep(500,tether);

            }
        }

        private static Unit _fountain;
        private static void AutoSaver(EventArgs args)
        {
            if (!IsEnableAutoSaver)
                return;
            if (_fountain == null || !_fountain.IsValid)
            {
                _fountain = ObjectManager.GetEntities<Unit>()
                    .FirstOrDefault(
                        x => x != null && x.Team == ObjectManager.LocalHero.Team && x.ClassID == ClassID.CDOTA_Unit_Fountain);
                return;
            }
            var tether = Abilities.FindAbility("wisp_tether");
            if (tether == null || tether.Level == 0 || !tether.CanBeCasted() || tether.IsHidden)
                return;
            var relocate = Abilities.FindAbility("wisp_relocate");
            if (relocate == null || relocate.Level == 0 || !relocate.CanBeCasted())
                return;
            var anyPossibleAlly =
                Heroes.GetByTeam(Members.MyTeam)
                    .FirstOrDefault(
                        x =>
                            x!=null && x.IsValid && Helper.IsHeroEnableForRelocate(x.StoredName()) &&
                            Helper.CheckForPercents(x) &&
                            x.Distance2D(Members.MyHero) <= tether.CastRange);
            
            if (anyPossibleAlly == null)
                return;

            if (!_multiSleeper.Sleeping(relocate))
            {
                relocate.UseAbility(_fountain.Position);
                tether.UseAbility(anyPossibleAlly);
                _multiSleeper.Sleep(500, relocate);

            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (!IsEnable)
                return;
            if (IsComboAim)
            {
                var startPos = new Vector2(Drawing.Width - 250, 100);
                var size = new Vector2(180, 90);
                Drawing.DrawRect(startPos, size, new Color(0, 0, 0, 100));
                Drawing.DrawRect(startPos, size, new Color(0, 0, 0, 255), true);
                Drawing.DrawText(
                    "Spirits Aim is Active" +
                    $"[{Utils.KeyToText(HeroAimHotkey)}]",
                    startPos + new Vector2(10, 10), new Vector2(20), new Color(0, 155, 255),
                    FontFlags.AntiAlias | FontFlags.DropShadow | FontFlags.Additive | FontFlags.Custom |
                    FontFlags.StrikeOut);
                if (_globalTarget2 != null && _globalTarget2.IsAlive)
                {
                    var pos = Drawing.WorldToScreen(_globalTarget2.Position);
                    Drawing.DrawText("Aim Target", pos, new Vector2(0, 50), Color.Red,
                        FontFlags.AntiAlias | FontFlags.DropShadow);
                    var name = "materials/ensage_ui/heroes_horizontal/" +
                               _globalTarget2.Name.Replace("npc_dota_hero_", "") + ".vmat";
                    size = new Vector2(50, 50);
                    Drawing.DrawRect(startPos + new Vector2(10, 35), size + new Vector2(13, -6),
                        Textures.GetTexture(name));
                    Drawing.DrawRect(startPos + new Vector2(10, 35), size + new Vector2(14, -5),
                        new Color(0, 0, 0, 255), true);
                }
            }
            if (IsComboHero)
            {
                var size = new Vector2(180, 90);
                var startPos = new Vector2(Drawing.Width - 250, 100);
                startPos += new Vector2(0, size.Y);
                Drawing.DrawRect(startPos, size, new Color(0, 0, 0, 100));
                Drawing.DrawRect(startPos, size, new Color(0, 0, 0, 255), true);
                Drawing.DrawText(
                    "Spirits Aim is Active" +
                    $"[{Utils.KeyToText(HeroHotkey)}]",
                    startPos + new Vector2(10, 10), new Vector2(20), new Color(0, 155, 255),
                    FontFlags.AntiAlias | FontFlags.DropShadow | FontFlags.Additive | FontFlags.Custom |
                    FontFlags.StrikeOut);
                if (_globalTarget != null && _globalTarget.IsAlive)
                {
                    var pos = Drawing.WorldToScreen(_globalTarget.Position);
                    Drawing.DrawText("Target", pos, new Vector2(0, 50), Color.Red,
                        FontFlags.AntiAlias | FontFlags.DropShadow);
                    var name = "materials/ensage_ui/heroes_horizontal/" +
                               _globalTarget.Name.Replace("npc_dota_hero_", "") + ".vmat";
                    size = new Vector2(50, 50);
                    Drawing.DrawRect(startPos + new Vector2(10, 35), size + new Vector2(13, -6),
                        Textures.GetTexture(name));
                    Drawing.DrawRect(startPos + new Vector2(10, 35), size + new Vector2(14, -5),
                        new Color(0, 0, 0, 255), true);
                }
            }
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (!IsEnable)
                return;
            Refresh();
            DoCombo();
            DoSpiritsAim();
        }

        private static void DoSpiritsAim()
        {
            if (!IsComboAim)
            {
                _globalTarget2 = null;
                return;
            }
            if (_globalTarget2 == null || !_globalTarget2.IsValid || !_globalTarget2.IsAlive)
            {
                _globalTarget2 = Helper.ClosestToMouse(Members.MyHero);
                return;
            }
            _trackList = _trackList.Where(x => !x.Ef.IsDestroyed).ToList();
            var wispList = _trackList;//ObjectManager.GetEntities<Unit>().Where(x => x.ClassID == ClassID.CDOTA_Wisp_Spirit && x.Team==Members.MyHero.Team && x.IsAlive).ToList();
            if (!wispList.Any())
                return;
            var spellIn = Abilities.FindAbility("wisp_spirits_in");
            var spellOut = Abilities.FindAbility("wisp_spirits_out");
            //var spirits = Abilities.FindAbility("wisp_spirits");
            if (spellIn.IsHidden || spellOut.IsHidden)
                return;
            var firstWisp = wispList.First();
            /*Printer.Print($"----{wispList.Count}--------");
            foreach (var unit in wispList)
            {
                Printer.Print($"{unit.Health}/{unit.IsAlive}/{unit.UnitState}/{unit.Modifiers.Any()}");
                var str=new StringBuilder();
                foreach (var modifier in unit.Modifiers)
                {
                    str.Append("/" + modifier.Name);
                }
                Printer.Print(str.ToString());
            }*/
            
            var distanceWithWisp = firstWisp.V.Distance2D(Members.MyHero);
            var distanceWithTarget = Members.MyHero.Distance2D(_globalTarget2);
            if (Math.Abs(distanceWithWisp- distanceWithTarget)<=50)
                if (!_multiSleeper.Sleeping("off") && (spellIn.IsToggled || spellOut.IsToggled))
                {
                    if (spellIn.IsToggled)
                        spellIn.ToggleAbility();
                    else
                        spellOut.ToggleAbility();
                    _multiSleeper.Sleep(250,"off");
                    _multiSleeper.Sleep(250, spellIn);
                    _multiSleeper.Sleep(250, spellOut);
                    return;
                }
            if (distanceWithWisp > distanceWithTarget)
            {
                if (!_multiSleeper.Sleeping(spellIn) && !spellIn.IsToggled)
                {
                    spellIn.ToggleAbility();
                    //Printer.Print("spellIn");
                    _multiSleeper.Sleep(250, spellIn);
                }
            }
            else
            {
                if (!_multiSleeper.Sleeping(spellOut) && !spellOut.IsToggled)
                {
                    spellOut.ToggleAbility();
                    //Printer.Print("spellOut");
                    _multiSleeper.Sleep(250, spellOut);
                }
            }
        }

        private static Hero _globalTarget;
        private static Hero _globalTarget2;
        private static Sleeper _attacker;
        private static Sleeper _updater;
        private static MultiSleeper _multiSleeper;

        private static void DoCombo()
        {
            if (!IsComboHero)
            {
                _globalTarget = null;
                return;
            }
            if (_globalTarget == null || !_globalTarget.IsValid || !_globalTarget.IsAlive)
            {
                _globalTarget = Helper.ClosestToMouse(Members.MyHero);
                return;
            }
            if (UseOrbWalkker)
            {
                Orbwalking.Orbwalk(_globalTarget, followTarget: true);
            }
            else if (!Members.MyHero.IsAttacking() && !_attacker.Sleeping)
            {
                Members.MyHero.Attack(_globalTarget);
                _attacker.Sleep(250);
            }

            var inventory =
                Members.MyHero.Inventory.Items.Where(
                    x =>
                        Helper.IsItemEnable(x.StoredName()) && x.CanBeCasted() &&
                        (x.GetCastRange() > 0 && Members.MyHero.Distance2D(_globalTarget) <= x.GetCastRange() + 150 ||
                        (x.GetCastRange() <= 0)) && Utils.SleepCheck($"{x.Handle}+item_usages"))
                    .ToList();
            foreach (var item in inventory)
            {
                if (item.IsAbilityBehavior(AbilityBehavior.NoTarget))
                    item.UseAbility();
                else if (item.IsAbilityBehavior(AbilityBehavior.UnitTarget))
                    if (item.TargetTeamType == TargetTeamType.Enemy || item.TargetTeamType == TargetTeamType.All ||
                        item.TargetTeamType == TargetTeamType.Custom)
                        item.UseAbility(_globalTarget);
                    else
                        item.UseAbility(Members.MyHero);
                else
                    item.UseAbility(_globalTarget.NetworkPosition);
                Utils.Sleep(250, $"{item.Handle}+item_usages");
            }
        }

        private static void Refresh()
        {
            
            if (_updater.Sleeping)
                return;
            _updater.Sleep(500);
            var inventory = Members.MyHero.Inventory.Items;
            var needToUpdate = false;
            foreach (var item in inventory)
            {
                if (!Members.Items.Contains(item.StoredName()) &&
                    (item.IsDisable() || item.IsNuke() || item.IsPurge() || item.IsHeal() || item.IsShield() || item.IsSilence() || item.IsSlow() || item.IsSkillShot()))
                {
                    Members.Items.Add(item.StoredName());
                    needToUpdate = true;
                    Printer.Print($"[NewItem]: {item.StoredName()}");
                }
            }
            if (needToUpdate)
                Members.Menu.Item("itemEnable")
                    .SetValue(new AbilityToggler(Members.Items.ToDictionary(x => x, y => true)));
        }
    }
}
