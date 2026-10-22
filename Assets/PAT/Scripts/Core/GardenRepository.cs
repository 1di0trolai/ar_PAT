using System;
using System.IO;
using UnityEngine;

namespace Pat.ArLab
{
    public sealed class GardenRepository
    {
        private readonly string path;
        public LabSave Data { get; private set; }
        public string LoadWarning { get; private set; }
        public string SavePath => path;

        public GardenRepository(string path)
        {
            this.path = path;
            if (File.Exists(path))
            {
                try
                {
                    Data = JsonUtility.FromJson<LabSave>(File.ReadAllText(path));
                    if (Data == null || Data.version != 1 || Data.plants == null || Data.pets == null || Data.hunts == null)
                        throw new InvalidDataException("Unsupported or incomplete save.");
                    ValidateGarden(Data);
                }
                catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is InvalidDataException)
                {
                    // Preserve damaged data; never silently overwrite the user's old garden.
                    File.Copy(path, path + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
                    LoadWarning = "Dữ liệu cũ không đọc được; đã giữ bản sao để kiểm tra.";
                    Data = LabCatalog.CreateDemo();
                }
            }
            else Data = LabCatalog.CreateDemo();
        }

        private static void ValidateGarden(LabSave data)
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            var markers = new System.Collections.Generic.HashSet<string>();
            foreach (var plant in data.plants)
                if (plant == null || string.IsNullOrWhiteSpace(plant.id) || !LabCatalog.IsKnownMarker(plant.markerId)
                    || !ids.Add(plant.id) || !markers.Add(plant.markerId))
                    throw new InvalidDataException("Duplicate or invalid garden mapping.");
        }

        public GardenPlant FindByMarker(string markerId) => Data.plants.Find(plant => plant.markerId == markerId);

        public GardenPlant Register(string markerId, string name, string species)
        {
            if (!LabCatalog.IsKnownMarker(markerId)) throw new ArgumentException("Nhãn không thuộc bộ thử nghiệm.");
            if (FindByMarker(markerId) != null) throw new InvalidOperationException("Nhãn đã được liên kết với một cây.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Hãy nhập tên cây.");
            var before = JsonUtility.ToJson(Data);
            var plant = new GardenPlant
            {
                id = Guid.NewGuid().ToString("N"), markerId = markerId,
                name = name.Trim(), species = string.IsNullOrWhiteSpace(species) ? "Cây của tôi" : species.Trim()
            };
            Data.plants.Add(plant);
            SaveOrRestore(before);
            return plant;
        }

        public void RemovePlant(string id)
        {
            var before = JsonUtility.ToJson(Data);
            Data.plants.RemoveAll(plant => plant.id == id);
            SaveOrRestore(before);
        }

        public bool UsedToday(DateTimeOffset now) => Data.hunts.Exists(hunt => !hunt.practice && hunt.localDate == LocalDay(now));
        public static string LocalDay(DateTimeOffset now) => now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");

        public HuntReceipt RecordCatch(string requestId, string plantId, string petId, bool practice, DateTimeOffset now)
        {
            var replay = Data.hunts.Find(hunt => hunt.requestId == requestId);
            if (replay != null)
            {
                if (replay.plantId != plantId || replay.petId != petId || replay.practice != practice)
                    throw new InvalidOperationException("Request trùng nhưng kết quả khác nhau.");
                return replay;
            }
            if (string.IsNullOrWhiteSpace(requestId) || !Data.plants.Exists(plant => plant.id == plantId))
                throw new InvalidOperationException("Cây không có trong vườn.");
            if (!Array.Exists(LabCatalog.Pets, pet => pet.id == petId)) throw new ArgumentException("Pet không hợp lệ.");
            if (!practice && UsedToday(now)) throw new InvalidOperationException("Bạn đã dùng lượt săn chung hôm nay.");
            var before = JsonUtility.ToJson(Data);
            var receipt = new HuntReceipt
            {
                requestId = requestId, plantId = plantId, petId = petId, practice = practice,
                localDate = LocalDay(now), completedAtUtc = now.UtcDateTime.ToString("O")
            };
            Data.hunts.Add(receipt);
            // Practice never changes the collection or consumes the real daily attempt.
            if (!practice)
            {
                var owned = Data.pets.Find(pet => pet.petId == petId);
                if (owned == null) Data.pets.Add(new OwnedPet { petId = petId, quantity = 1 });
                else owned.quantity++;
            }
            SaveOrRestore(before);
            return receipt;
        }

        private void SaveOrRestore(string before)
        {
            try { Save(); }
            catch { Data = JsonUtility.FromJson<LabSave>(before); throw; }
        }

        public void Save()
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(Data, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
