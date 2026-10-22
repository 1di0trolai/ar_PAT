using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Pat.ArLab.Tests
{
    public sealed class HuntRulesTests
    {
        private string directory;
        private string path;
        private readonly DateTimeOffset today = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "pat-ar-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "garden.json");
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void TenSecondsOfValidTrackingAreRequired()
        {
            var session = new HuntSession(10);
            Assert.That(session.Begin("plant-map"), Is.True);
            for (var i = 0; i < 90; i++) session.Tick(0.1f, true, "");
            Assert.That(session.CanCatch, Is.False);
            for (var i = 0; i < 20; i++) session.Tick(0.1f, true, "");
            Assert.That(session.CanCatch, Is.True);
            Assert.That(session.Complete(), Is.True);
            Assert.That(session.Complete(), Is.False, "A double press cannot complete twice.");
        }

        [Test]
        public void LostTrackingNeverAdvancesScanAndBlocksCatch()
        {
            var session = new HuntSession(1);
            session.Begin("plant-map");
            for (var i = 0; i < 100; i++) session.Tick(0.1f, false, "lost tracking");
            Assert.That(session.ScanSeconds, Is.Zero);
            Assert.That(session.Paused, Is.True);
            for (var i = 0; i < 12; i++) session.Tick(0.1f, true, "");
            session.Tick(0.1f, false, "lost tracking");
            Assert.That(session.CanCatch, Is.False);
            session.Tick(0.01f, true, "");
            Assert.That(session.CanCatch, Is.True);
        }

        [Test]
        public void AppStallCannotFastForwardScan()
        {
            var session = new HuntSession(10);
            session.Begin("plant-map");
            session.Tick(60, true, "");
            session.Tick(float.PositiveInfinity, true, "");
            session.Tick(float.NaN, true, "");
            Assert.That(session.ScanSeconds, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void LockedTargetCannotSwitchToAnotherPlant()
        {
            var session = new HuntSession(10);
            session.Begin("plant-map");
            Assert.That(session.Begin("plant-mint"), Is.False);
            Assert.That(session.PlantId, Is.EqualTo("plant-map"));
            session.Reset();
            Assert.That(session.Begin("plant-mint"), Is.True);
        }

        [Test]
        public void UnregisteredMarkersCannotOpenGardenPlant()
        {
            var repository = new GardenRepository(path);
            Assert.That(repository.FindByMarker("PAT-001").name, Is.EqualTo("Bé Mập"));
            Assert.That(repository.FindByMarker("PAT-004"), Is.Null);
            Assert.That(repository.FindByMarker("a-random-pot"), Is.Null);
        }

        [Test]
        public void MarkerMappingIsUniqueAndPersistsAcrossRestarts()
        {
            var repository = new GardenRepository(path);
            var added = repository.Register("PAT-004", "Sen Đá", "Succulent");
            Assert.Throws<InvalidOperationException>(() => repository.Register("PAT-004", "Cây khác", ""));
            var reopened = new GardenRepository(path);
            Assert.That(reopened.FindByMarker("PAT-004").id, Is.EqualTo(added.id));
            reopened.RemovePlant(added.id);
            Assert.That(new GardenRepository(path).FindByMarker("PAT-004"), Is.Null);
        }

        [Test]
        public void ArbitraryMarkerCannotBeRegistered()
        {
            Assert.Throws<ArgumentException>(() => new GardenRepository(path).Register("not-in-library", "Cây", ""));
        }

        [Test]
        public void DailyQuotaIsSharedAcrossPlantsAndSurvivesRestart()
        {
            var repository = new GardenRepository(path);
            repository.RecordCatch("first", "plant-map", "ladybug", false, today);
            var reopened = new GardenRepository(path);
            Assert.Throws<InvalidOperationException>(() => reopened.RecordCatch("second", "plant-mint", "hunter-bee", false, today));
            Assert.That(reopened.Data.hunts.Count, Is.EqualTo(1));
            Assert.That(reopened.Data.pets.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateCaptureRequestOnlyAwardsOnePet()
        {
            var repository = new GardenRepository(path);
            var first = repository.RecordCatch("same", "plant-map", "ladybug", false, today);
            var replay = repository.RecordCatch("same", "plant-map", "ladybug", false, today);
            Assert.That(replay, Is.SameAs(first));
            Assert.That(repository.Data.pets[0].quantity, Is.EqualTo(1));
            Assert.That(repository.Data.hunts.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => repository.RecordCatch("same", "plant-mint", "ladybug", false, today));
        }

        [Test]
        public void PracticeDoesNotConsumeQuotaOrAwardInventory()
        {
            var repository = new GardenRepository(path);
            repository.RecordCatch("practice", "plant-map", "ladybug", true, today);
            repository.RecordCatch("practice2", "plant-mint", "hunter-bee", true, today);
            Assert.That(repository.UsedToday(today), Is.False);
            Assert.That(repository.Data.pets, Is.Empty);
            repository.RecordCatch("real", "plant-map", "ladybug", false, today);
            Assert.That(repository.UsedToday(today), Is.True);
        }

        [Test]
        public void UnownedPlantNeverAwardsPet()
        {
            var repository = new GardenRepository(path);
            Assert.Throws<InvalidOperationException>(() => repository.RecordCatch("invalid", "a-stranger-plant", "ladybug", false, today));
            Assert.That(repository.Data.hunts, Is.Empty);
            Assert.That(repository.Data.pets, Is.Empty);
        }

        [Test]
        public void VietnamMidnightResetsDailyQuota()
        {
            var repository = new GardenRepository(path);
            var before = new DateTimeOffset(2026, 10, 22, 16, 59, 59, TimeSpan.Zero);
            var after = before.AddSeconds(1);
            repository.RecordCatch("before", "plant-map", "ladybug", false, before);
            Assert.That(repository.UsedToday(before), Is.True);
            Assert.That(repository.UsedToday(after), Is.False);
            repository.RecordCatch("after", "plant-mint", "ladybug", false, after);
            Assert.That(repository.Data.pets[0].quantity, Is.EqualTo(2));
        }

        [Test]
        public void DamagedSaveIsPreservedRatherThanSilentlyDeleted()
        {
            File.WriteAllText(path, "{bad json");
            var repository = new GardenRepository(path);
            Assert.That(repository.LoadWarning, Is.Not.Empty);
            Assert.That(Directory.GetFiles(directory, "garden.json.invalid-*").Length, Is.EqualTo(1));
            Assert.That(repository.Data.plants.Count, Is.EqualTo(3));
        }

        [Test]
        public void AllSixMarkersHaveDistinctNamesAndPhysicalSizes()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(Editor.LabProjectBuilder.LibraryPath);
            Assert.That(library, Is.Not.Null, "Run PAT > Setup or repair AR Lab.");
            Assert.That(library.count, Is.EqualTo(6));
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var image in library)
            {
                Assert.That(names.Add(image.name), Is.True);
                Assert.That(image.specifySize, Is.True);
                Assert.That(image.size.x, Is.EqualTo(0.12f).Within(0.001f));
            }
        }

        [Test]
        public void BothMobilePlatformsHaveTheCorrectXrProvider()
        {
            var android = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var ios = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
            Assert.That(android.Manager.activeLoaders[0].GetType().Name, Is.EqualTo("ARCoreLoader"));
            Assert.That(ios.Manager.activeLoaders[0].GetType().Name, Is.EqualTo("ARKitLoader"));
        }
    }
}
