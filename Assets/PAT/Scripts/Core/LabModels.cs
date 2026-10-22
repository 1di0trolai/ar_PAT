using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pat.ArLab
{
    [Serializable]
    public sealed class GardenPlant
    {
        public string id;
        public string markerId;
        public string name;
        public string species;
    }

    [Serializable]
    public sealed class OwnedPet
    {
        public string petId;
        public int quantity;
    }

    [Serializable]
    public sealed class HuntReceipt
    {
        public string requestId;
        public string plantId;
        public string petId;
        public string localDate;
        public string completedAtUtc;
        public bool practice;
    }

    [Serializable]
    public sealed class LabSave
    {
        public int version = 1;
        public string userId = "pat-lab-user";
        public List<GardenPlant> plants = new List<GardenPlant>();
        public List<OwnedPet> pets = new List<OwnedPet>();
        public List<HuntReceipt> hunts = new List<HuntReceipt>();
    }

    [Serializable]
    public sealed class PetDefinition
    {
        public string id;
        public string name;
        public string rarity;
        public Color color;
        public float weight;
    }

    public static class LabCatalog
    {
        public const float MarkerWidthMeters = 0.12f;
        public const int MarkerCount = 6;

        public static readonly PetDefinition[] Pets =
        {
            new PetDefinition { id = "ladybug", name = "Bọ Rùa Lửa", rarity = "Hiếm", color = new Color(0.94f, 0.31f, 0.29f), weight = 25 },
            new PetDefinition { id = "caterpillar", name = "Sâu Phàm Ăn", rarity = "Thường", color = new Color(0.58f, 0.78f, 0.32f), weight = 55 },
            new PetDefinition { id = "hunter-bee", name = "Ong Thợ Săn", rarity = "Hiếm", color = new Color(1f, 0.73f, 0.22f), weight = 20 }
        };

        public static string MarkerId(int index) => $"PAT-{index:000}";

        public static bool IsKnownMarker(string id)
        {
            for (var i = 1; i <= MarkerCount; i++)
                if (MarkerId(i) == id) return true;
            return false;
        }

        public static PetDefinition RollPet(float unitRandom)
        {
            var total = 0f;
            foreach (var pet in Pets) total += pet.weight;
            var value = Mathf.Clamp01(unitRandom) * total;
            foreach (var pet in Pets)
            {
                value -= pet.weight;
                if (value < 0f) return pet;
            }
            return Pets[Pets.Length - 1];
        }

        public static LabSave CreateDemo()
        {
            var save = new LabSave();
            save.plants.Add(new GardenPlant { id = "plant-map", markerId = MarkerId(1), name = "Bé Mập", species = "Monstera" });
            save.plants.Add(new GardenPlant { id = "plant-mint", markerId = MarkerId(2), name = "Minty", species = "Bạc hà" });
            save.plants.Add(new GardenPlant { id = "plant-cactus", markerId = MarkerId(3), name = "Gai Nhỏ", species = "Xương rồng" });
            return save;
        }
    }
}
