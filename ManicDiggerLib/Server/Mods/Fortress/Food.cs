using System;

namespace ManicDigger.Mods
{
	public class Food : IMod
	{
		public void PreStart(IModManager m)
		{
			m.RequireMod("CoreBlocks");
		}
		public void Start(IModManager manager)
		{
			m = manager;
			
			m.RegisterOnBlockUse(OnUse);
			
			Cake = m.GetBlockId("Cake");
			Apples = m.GetBlockId("Apples");
		}
		IModManager m;
		int Cake;
		int Apples;

		void OnUse(int player, int x, int y, int z)
		{
			if (m.GetBlock(x, y, z) == Cake || m.GetBlock(x, y, z) == Apples)
			{
				int health = m.GetPlayerHealth(player);
				int maxhealth = m.GetPlayerMaxHealth(player);

				health += 30;

				if (health > maxhealth)
				{
					health = maxhealth;
				}

				m.SetPlayerHealth(player, health, maxhealth);
				m.SetBlock(x, y, z, 0);
			}
		}
	}
}
