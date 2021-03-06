﻿using System;
using System.Collections.Generic;
using Aurora.EffectsEngine;
using Aurora.Profiles.Payday_2.GSI;
using System.Drawing;
using Aurora.Profiles.Payday_2.GSI.Nodes;
using System.Linq;

namespace Aurora.Profiles.Payday_2
{
    public class GameEvent_PD2 : LightEvent
    {
        public GameEvent_PD2() : base()
        {
        }

        public override void UpdateLights(EffectFrame frame)
        {
            Queue<EffectLayer> layers = new Queue<EffectLayer>();

            PD2Profile settings = (PD2Profile)this.Application.Profile;

            foreach (var layer in Application.Profile.Layers.Reverse().ToArray())
            {
                if (layer.Enabled && layer.LogicPass)
                    layers.Enqueue(layer.Render(_game_state));
            }

            //Scripts
            this.Application.UpdateEffectScripts(layers, _game_state);

            //ColorZones
            layers.Enqueue(new EffectLayer("PD2 - Color Zones").DrawColorZones((this.Application.Profile as PD2Profile).lighting_areas.ToArray()));

            frame.AddLayers(layers.ToArray());
        }

        public override void SetGameState(IGameState new_game_state)
        {
            if (new_game_state is GameState_PD2)
            {
                _game_state = new_game_state;
            }
        }

        public override void ResetGameState()
        {
            _game_state = new GameState_PD2();
        }
    }
}
