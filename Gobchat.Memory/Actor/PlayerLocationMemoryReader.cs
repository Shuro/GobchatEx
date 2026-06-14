/*******************************************************************************
 * Copyright (C) 2019-2025 MarbleBag
 * Copyright (C) 2026 Shuro
 *
 * This program is free software: you can redistribute it and/or modify it under
 * the terms of the GNU Affero General Public License as published by the Free
 * Software Foundation, version 3.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 *******************************************************************************/

using Sharlayan.Core;
using System.Collections.Generic;

namespace Gobchat.Memory.Actor
{
    internal sealed class PlayerLocationMemoryReader
    {
        private readonly ProcessConnector _connector;

        public PlayerLocationMemoryReader(ProcessConnector connector)
        {
            _connector = connector;
        }

        public bool LocationAvailable => _connector.ActiveHandler?.Reader.CanGetActors() == true;

        private void Process(ICollection<ActorItem> actors, PlayerCharacter.UpdateFlag flag, Coordinate? mainActorPosition, ICollection<PlayerCharacter> results)
        {
            foreach (var actor in actors)
            {
                if (!(actor.IsValid && actor.Type == Sharlayan.Core.Enums.Actor.Type.PC))
                    continue;

                var data = new PlayerCharacter()
                {
                    Name = actor.Name,
                    Id = actor.ID,
                    UId = actor.UUID,
                    Flag = flag,
                    SimplifiedDistanceToPlayer = actor.Distance,
                };

                if (mainActorPosition != null)
                    data.DistanceToPlayer = mainActorPosition.DistanceTo(actor.Coordinate);

                results.Add(data);
            }
        }

        public List<PlayerCharacter> GetPlayerCharacters()
        {
            var handler = _connector.ActiveHandler;
            if (handler == null)
                return new List<PlayerCharacter>();

            var result = new List<PlayerCharacter>();
            var memoryResult = handler.Reader.GetActors();

            // The active player is resolved once per poll and reused for position + IsUser marking.
            var currentUser = handler.Reader.GetCurrentPlayer()?.Entity;
            var activePlayerPosition = (currentUser != null && currentUser.IsValid) ? currentUser.Coordinate : null;

            Process(memoryResult.CurrentPCs.Values, PlayerCharacter.UpdateFlag.Update, activePlayerPosition, result);
            Process(memoryResult.RemovedPCs.Values, PlayerCharacter.UpdateFlag.Remove, activePlayerPosition, result);
            Process(memoryResult.NewPCs.Values, PlayerCharacter.UpdateFlag.New, activePlayerPosition, result);
            MarkActivePlayer(result, currentUser);

            return result;
        }

        private void MarkActivePlayer(List<PlayerCharacter> characters, ActorItem? currentUser)
        {
            if (currentUser == null)
                return;

            foreach (var character in characters)
            {
                if (character.Id == currentUser.ID)
                {
                    character.IsUser = true;
                    break;
                }
            }
        }
    }
}
