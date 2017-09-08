﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using Newtonsoft.Json.Linq;

namespace Aura.Data.Database
{
	[Serializable]
	public class ChairData
	{
		public int ItemId { get; set; }
		public int PropId { get; set; }
		public int GiantPropId { get; set; }
		public int Effect { get; set; }
		public string State { get; set; }
		public string NextState { get; set; }
		public int StateChangeDelay { get; set; }
		public int Stand { get; set; }
	}

	/// <summary>
	/// Indexed by item id.
	/// </summary>
	public class ChairDb : DatabaseJsonIndexed<int, ChairData>
	{
		protected override void ReadEntry(JObject entry)
		{
			entry.AssertNotMissing("itemId", "propId", "giantPropId");

			var info = new ChairData();
			info.ItemId = entry.ReadInt("itemId");
			info.PropId = entry.ReadInt("propId");
			info.GiantPropId = entry.ReadInt("giantPropId");
			info.Effect = entry.ReadInt("effect");
			info.State = entry.ReadString("state", "stand");
			info.NextState = entry.ReadString("nextState", null);
			info.StateChangeDelay = entry.ReadInt("stateChangeDelay", 0);
			info.Stand = entry.ReadInt("stand", -1);

			this.Entries[info.ItemId] = info;
		}
	}
}
