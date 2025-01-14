﻿using System;
using System.Collections.Generic;
using Content.Shared.Damage.Components;

namespace Content.Shared.Damage
{
    public class DamageChangedEventArgs : EventArgs
    {
        public DamageChangedEventArgs(IDamageableComponent damageable, IReadOnlyList<DamageChangeData> data)
        {
            Damageable = damageable;
            Data = data;
        }

        public DamageChangedEventArgs(IDamageableComponent damageable, DamageTypePrototype type, int newValue, int delta)
        {
            Damageable = damageable;

            var datum = new DamageChangeData(type, newValue, delta);
            var data = new List<DamageChangeData> {datum};

            Data = data;
        }

        /// <summary>
        ///     Reference to the <see cref="IDamageableComponent"/> that invoked the event.
        /// </summary>
        public IDamageableComponent Damageable { get; }

        /// <summary>
        ///     List containing data on each <see cref="DamageTypePrototype"/> that was changed.
        /// </summary>
        public IReadOnlyList<DamageChangeData> Data { get; }
    }
}
