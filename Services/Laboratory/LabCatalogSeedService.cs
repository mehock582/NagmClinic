using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;

namespace NagmClinic.Services.Laboratory
{
    public interface ILabCatalogSeedService
    {
        Task SeedDefaultsAsync();
        Task<int> SeedEc38TestsAsync();
        Task<int> SeedLansionbioAndEs100cTestsAsync();
    }

    public class LabCatalogSeedService : ILabCatalogSeedService
    {
        private const string Ec38AnalyzerCode = "EC38";
        private const string LansionbioAnalyzerCode = "LANSIONBIO";
        private const string Es100CAnalyzerCode = "ES-100C";

        private readonly ApplicationDbContext _context;

        public LabCatalogSeedService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedDefaultsAsync()
        {
            await EnsureAnalyzerAsync();
            await EnsureLansionbioAnalyzerAsync();
            await EnsureEs100CAnalyzerAsync();
            await EnsureCategoriesAsync();
            await SeedEc38TestsAsync();
            await SeedLansionbioAndEs100cTestsAsync();
        }

        public async Task<int> SeedEc38TestsAsync()
        {
            var analyzer = await EnsureAnalyzerAsync();
            var categories = await EnsureCategoriesAsync();
            var existingTests = await _context.ClinicServices
                .Where(s => s.Type == ServiceType.LabTest && s.Code != null)
                .ToDictionaryAsync(s => s.Code!, s => s);

            var testsAdded = 0;
            foreach (var definition in GetEc38Definitions(categories, analyzer.Id))
            {
                if (existingTests.ContainsKey(definition.Code!))
                {
                    continue;
                }

                _context.ClinicServices.Add(definition);
                testsAdded++;
            }

            if (testsAdded > 0)
            {
                await _context.SaveChangesAsync();
            }

            return testsAdded;
        }

        public async Task<int> SeedLansionbioAndEs100cTestsAsync()
        {
            var lansionbioAnalyzer = await EnsureLansionbioAnalyzerAsync();
            var es100cAnalyzer = await EnsureEs100CAnalyzerAsync();
            var categories = await EnsureCategoriesAsync();

            var existingLabTests = await _context.ClinicServices
                .Where(s => s.Type == ServiceType.LabTest)
                .Select(s => new { s.Id, s.Code, s.DeviceCode, s.LabAnalyzerId })
                .ToListAsync();

            var usedCodes = existingLabTests
                .Where(t => !string.IsNullOrWhiteSpace(t.Code))
                .Select(t => t.Code!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingMappedPairs = existingLabTests
                .Where(t => t.LabAnalyzerId.HasValue && !string.IsNullOrWhiteSpace(t.DeviceCode))
                .Select(t => BuildMappedPairKey(t.LabAnalyzerId!.Value, t.DeviceCode!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var testsAdded = 0;
            var definitions = GetLansionbioDefinitions(categories, lansionbioAnalyzer.Id)
                .Concat(GetEs100cDefinitions(categories, es100cAnalyzer.Id));

            foreach (var definition in definitions)
            {
                var mappedPairKey = BuildMappedPairKey(definition.AnalyzerId, definition.DeviceCode);
                if (existingMappedPairs.Contains(mappedPairKey))
                {
                    continue;
                }

                var internalCode = ResolveUniqueCode(definition.DeviceCode, definition.InternalCodePrefix, usedCodes);
                var referenceRange = BuildRange(definition.MinValue, definition.MaxValue);

                var test = BuildMappedDeviceTest(
                    internalCode,
                    definition.DisplayNameAr,
                    definition.DisplayNameEn,
                    definition.PrintName,
                    definition.CategoryId,
                    definition.Unit,
                    referenceRange,
                    definition.SortOrder,
                    definition.AnalyzerId,
                    definition.DeviceCode,
                    definition.SampleType,
                    definition.Notes);

                _context.ClinicServices.Add(test);
                usedCodes.Add(internalCode);
                existingMappedPairs.Add(mappedPairKey);
                testsAdded++;
            }

            if (testsAdded > 0)
            {
                await _context.SaveChangesAsync();
            }

            return testsAdded;
        }

        private async Task<LabAnalyzer> EnsureAnalyzerAsync()
        {
            var analyzer = await _context.LabAnalyzers.FirstOrDefaultAsync(a => a.Code == Ec38AnalyzerCode);
            if (analyzer != null)
            {
                return analyzer;
            }

            analyzer = new LabAnalyzer
            {
                Name = "Bioelab EC-38",
                Code = Ec38AnalyzerCode,
                Manufacturer = "Bioelab",
                WholeBloodSampleVolume = "9 uL",
                PredilutedSampleVolume = "20 uL",
                Notes = "CBC analyzer. WBC/RBC/HGB carryover <0.5%."
            };

            _context.LabAnalyzers.Add(analyzer);
            await _context.SaveChangesAsync();
            return analyzer;
        }

        private async Task<Dictionary<string, LabCategory>> EnsureCategoriesAsync()
        {
            var categorySeeds = new[]
            {
                new LabCategory { NameAr = "أمراض الدم", NameEn = "Hematology", SortOrder = 1 },
                new LabCategory { NameAr = "لوحة كريات الدم البيضاء", NameEn = "WBC Panel", SortOrder = 2 },
                new LabCategory { NameAr = "لوحة كريات الدم الحمراء", NameEn = "RBC Panel", SortOrder = 3 },
                new LabCategory { NameAr = "لوحة الصفائح الدموية", NameEn = "Platelet Panel", SortOrder = 4 },
                new LabCategory { NameAr = "السكري", NameEn = "Diabetes", SortOrder = 10 },
                new LabCategory { NameAr = "الالتهابات", NameEn = "Inflammation", SortOrder = 11 },
                new LabCategory { NameAr = "مؤشرات القلب", NameEn = "Cardiac", SortOrder = 12 },
                new LabCategory { NameAr = "الهرمونات", NameEn = "Hormones", SortOrder = 13 },
                new LabCategory { NameAr = "تحاليل المعدة", NameEn = "Gastric", SortOrder = 14 },
                new LabCategory { NameAr = "تحاليل الكلى", NameEn = "Renal", SortOrder = 15 },
                new LabCategory { NameAr = "التخثر", NameEn = "Coagulation", SortOrder = 16 },
                new LabCategory { NameAr = "تحاليل متنوعة", NameEn = "Other", SortOrder = 17 }
            };

            var existing = await _context.LabCategories.ToListAsync();
            foreach (var seed in categorySeeds)
            {
                if (existing.Any(c => c.NameAr == seed.NameAr))
                {
                    continue;
                }

                _context.LabCategories.Add(seed);
                existing.Add(seed);
            }

            await _context.SaveChangesAsync();

            return existing.ToDictionary(c => c.NameEn ?? c.NameAr, c => c);
        }

        private async Task<LabAnalyzer> EnsureLansionbioAnalyzerAsync()
        {
            var analyzer = await _context.LabAnalyzers.FirstOrDefaultAsync(a => a.Code == LansionbioAnalyzerCode);
            if (analyzer != null)
            {
                return analyzer;
            }

            analyzer = new LabAnalyzer
            {
                Name = "Lansionbio Multi-Panel Analyzer",
                Code = LansionbioAnalyzerCode,
                Manufacturer = "Lansionbio",
                Notes = "POCT analyzer for diabetes, inflammation, hormones, renal, and cardiac markers."
            };

            _context.LabAnalyzers.Add(analyzer);
            await _context.SaveChangesAsync();
            return analyzer;
        }

        private async Task<LabAnalyzer> EnsureEs100CAnalyzerAsync()
        {
            var analyzer = await _context.LabAnalyzers.FirstOrDefaultAsync(a => a.Code == Es100CAnalyzerCode);
            if (analyzer != null)
            {
                return analyzer;
            }

            analyzer = new LabAnalyzer
            {
                Name = "Bioelab ES-100C",
                Code = Es100CAnalyzerCode,
                Manufacturer = "Bioelab",
                Notes = "Coagulation analyzer (PT/APTT/TT)."
            };

            _context.LabAnalyzers.Add(analyzer);
            await _context.SaveChangesAsync();
            return analyzer;
        }

        private static IEnumerable<ClinicService> GetEc38Definitions(
            IReadOnlyDictionary<string, LabCategory> categories,
            int analyzerId)
        {
            var hematology = categories["Hematology"];
            var wbcPanel = categories["WBC Panel"];
            var rbcPanel = categories["RBC Panel"];
            var plateletPanel = categories["Platelet Panel"];

            return new[]
            {
                BuildEc38Test("WBC", "عدد كريات الدم البيضاء (WBC)", "Total White Blood Cell Count", "WBC", wbcPanel.Id, "10^9/L", "4.0 - 10.0", "Adult: 4.0 - 10.0", 10, analyzerId),
                BuildEc38Test("LYM%", "نسبة الخلايا اللمفاوية", "Lymphocyte Percentage", "LYM%", wbcPanel.Id, "%", "20.0 - 40.0", "Adult: 20.0 - 40.0", 20, analyzerId),
                BuildEc38Test("MID%", "نسبة الخلايا المتوسطة", "Mid-sized Cell Percentage", "MID%", wbcPanel.Id, "%", "1.0 - 15.0", "Adult: 1.0 - 15.0", 30, analyzerId),
                BuildEc38Test("GRAN%", "نسبة الخلايا الحبيبية", "Granulocyte Percentage", "GRAN%", wbcPanel.Id, "%", "50.0 - 70.0", "Adult: 50.0 - 70.0", 40, analyzerId),
                BuildEc38Test("LYM#", "عدد الخلايا اللمفاوية", "Lymphocyte Count", "LYM#", wbcPanel.Id, "10^9/L", "0.6 - 4.1", "Adult: 0.6 - 4.1", 50, analyzerId),
                BuildEc38Test("MID#", "عدد الخلايا المتوسطة", "Mid-sized Cell Count", "MID#", wbcPanel.Id, "10^9/L", "0.1 - 1.8", "Adult: 0.1 - 1.8", 60, analyzerId),
                BuildEc38Test("GRAN#", "عدد الخلايا الحبيبية", "Granulocyte Count", "GRAN#", wbcPanel.Id, "10^9/L", "2.0 - 7.8", "Adult: 2.0 - 7.8", 70, analyzerId),

                BuildEc38Test("RBC", "عدد كريات الدم الحمراء", "Red Blood Cell Count", "RBC", rbcPanel.Id, "10^12/L", "Male: 4.3 - 5.8 / Female: 3.8 - 5.1", "Male: 4.3 - 5.8 / Female: 3.8 - 5.1", 110, analyzerId),
                BuildEc38Test("HGB", "الهيموغلوبين", "Hemoglobin", "HGB", rbcPanel.Id, "g/dL", "Male: 13.0 - 17.5 / Female: 11.5 - 15.5", "Male: 13.0 - 17.5 / Female: 11.5 - 15.5", 120, analyzerId),
                BuildEc38Test("HCT", "الهيماتوكريت", "Hematocrit", "HCT", rbcPanel.Id, "%", "Male: 40 - 50 / Female: 35 - 45", "Male: 40 - 50 / Female: 35 - 45", 130, analyzerId),
                BuildEc38Test("MCV", "متوسط حجم الكرية", "Mean Corpuscular Volume", "MCV", rbcPanel.Id, "fL", "82.0 - 100.0", "Adult: 82.0 - 100.0", 140, analyzerId),
                BuildEc38Test("MCH", "متوسط هيموغلوبين الكرية", "Mean Corpuscular Hemoglobin", "MCH", rbcPanel.Id, "pg", "27.0 - 34.0", "Adult: 27.0 - 34.0", 150, analyzerId),
                BuildEc38Test("MCHC", "تركيز هيموغلوبين الكرية", "MCH Concentration", "MCHC", rbcPanel.Id, "g/dL", "31.6 - 35.4", "Adult: 31.6 - 35.4", 160, analyzerId),
                BuildEc38Test("RDW-SD", "تباين الكريات الحمراء RDW-SD", "RBC Distribution Width - SD", "RDW-SD", rbcPanel.Id, "fL", "35.0 - 56.0", "Adult: 35.0 - 56.0", 170, analyzerId),
                BuildEc38Test("RDW-CV", "تباين الكريات الحمراء RDW-CV", "RBC Distribution Width - CV", "RDW-CV", rbcPanel.Id, "%", "11.0 - 16.0", "Adult: 11.0 - 16.0", 180, analyzerId),

                BuildEc38Test("PLT", "عدد الصفائح الدموية", "Platelet Count", "PLT", plateletPanel.Id, "10^9/L", "125 - 350", "Adult: 125 - 350", 210, analyzerId),
                BuildEc38Test("MPV", "متوسط حجم الصفيحة", "Mean Platelet Volume", "MPV", plateletPanel.Id, "fL", "7.0 - 11.0", "Adult: 7.0 - 11.0", 220, analyzerId),
                BuildEc38Test("PDW", "تباين حجم الصفائح", "Platelet Distribution Width", "PDW", plateletPanel.Id, "fL", "9.0 - 17.0", "Adult: 9.0 - 17.0", 230, analyzerId),
                BuildEc38Test("PCT", "هيماتوكريت الصفائح", "Plateletcrit", "PCT", plateletPanel.Id, "%", "0.10 - 0.35", "Adult: 0.10 - 0.35", 240, analyzerId),
                BuildEc38Test("P-LCR", "نسبة الصفائح كبيرة الحجم", "Platelet Large Cell Ratio", "P-LCR", plateletPanel.Id, "%", "13.0 - 43.0", "Adult: 13.0 - 43.0", 250, analyzerId),
                BuildEc38Test("P-LCC", "عدد الصفائح كبيرة الحجم", "Platelet Large Cell Count", "P-LCC", plateletPanel.Id, "10^9/L", "30 - 90", "Adult: 30 - 90", 260, analyzerId)
            }
            .Select(test =>
            {
                test.LabCategoryId ??= hematology.Id;
                return test;
            });
        }

        private static ClinicService BuildEc38Test(
            string code,
            string arabicName,
            string englishName,
            string printName,
            int categoryId,
            string unit,
            string normalRange,
            string referenceRange,
            int sortOrder,
            int analyzerId)
        {
            return new ClinicService
            {
                Code = code,
                NameAr = arabicName,
                NameEn = englishName,
                Type = ServiceType.LabTest,
                Price = 0m,
                Unit = unit,
                NormalRange = normalRange,
                ReferenceRange = referenceRange,
                ResultType = LabResultType.Number,
                SourceType = LabTestSourceType.Device,
                IsDeviceMapped = true,
                DeviceCode = code,
                SortOrder = sortOrder,
                PrintName = printName,
                SampleType = "Whole Blood",
                LabCategoryId = categoryId,
                LabAnalyzerId = analyzerId,
                IsActive = true,
                Notes = "Seeded from Bioelab EC-38 analyzer catalog."
            };
        }

        private static IEnumerable<DeviceTestSeedDefinition> GetLansionbioDefinitions(
            IReadOnlyDictionary<string, LabCategory> categories,
            int analyzerId)
        {
            return new[]
            {
                // Diabetes
                BuildDefinition("HBA1C", "HbA1c", "HbA1c", "HbA1c", "Diabetes", "%", 4.0m, 5.6m, 1010, analyzerId, "LS"),

                // Inflammation
                BuildDefinition("CRP", "C-Reactive Protein", "C-Reactive Protein", "CRP", "Inflammation", "mg/L", 0m, 5m, 1110, analyzerId, "LS"),
                BuildDefinition("PCT", "Procalcitonin", "Procalcitonin", "PCT", "Inflammation", "ng/mL", 0m, 0.05m, 1120, analyzerId, "LS"),
                BuildDefinition("SAA", "Serum Amyloid A", "Serum Amyloid A", "SAA", "Inflammation", "mg/L", 0m, 10m, 1130, analyzerId, "LS"),

                // Cardiac
                BuildDefinition("CKMB", "CK-MB", "CK-MB", "CK-MB", "Cardiac", "ng/mL", 0m, 5m, 1210, analyzerId, "LS"),
                BuildDefinition("CTNI", "Troponin I", "Troponin I", "cTnI", "Cardiac", "ng/mL", 0m, 0.04m, 1220, analyzerId, "LS"),
                BuildDefinition("MYO", "Myoglobin", "Myoglobin", "Myoglobin", "Cardiac", "ng/mL", 25m, 72m, 1230, analyzerId, "LS"),
                BuildDefinition("NTBNP", "NT-proBNP", "NT-proBNP", "NT-proBNP", "Cardiac", "pg/mL", 0m, 125m, 1240, analyzerId, "LS"),
                BuildDefinition("DDIMER", "D-Dimer", "D-Dimer", "D-Dimer", "Cardiac", "ug/mL", 0m, 0.5m, 1250, analyzerId, "LS"),
                BuildDefinition("HFABP", "H-FABP", "H-FABP", "H-FABP", "Cardiac", "ng/mL", 0m, 7m, 1260, analyzerId, "LS"),

                // Hormones
                BuildDefinition("T3", "Triiodothyronine", "Triiodothyronine", "T3", "Hormones", "ng/mL", 0.8m, 2.0m, 1310, analyzerId, "LS"),
                BuildDefinition("T4", "Thyroxine", "Thyroxine", "T4", "Hormones", "ug/dL", 5m, 12m, 1320, analyzerId, "LS"),
                BuildDefinition("TSH", "TSH", "TSH", "TSH", "Hormones", "uIU/mL", 0.4m, 4.0m, 1330, analyzerId, "LS"),
                BuildDefinition("VITD", "Vitamin D (25-OH)", "Vitamin D (25-OH)", "Vitamin D", "Hormones", "ng/mL", 30m, 100m, 1340, analyzerId, "LS"),
                BuildDefinition("BHCG", "Beta HCG", "Beta HCG", "Beta HCG", "Hormones", "mIU/mL", 0m, 5m, 1350, analyzerId, "LS"),
                BuildDefinition("LH", "Luteinizing Hormone", "Luteinizing Hormone", "LH", "Hormones", "IU/L", 1.5m, 9.3m, 1360, analyzerId, "LS"),
                BuildDefinition("FSH", "Follicle Stimulating Hormone", "Follicle Stimulating Hormone", "FSH", "Hormones", "IU/L", 1.4m, 18.1m, 1370, analyzerId, "LS"),
                BuildDefinition("GH", "Growth Hormone", "Growth Hormone", "GH", "Hormones", "ng/mL", 0m, 5m, 1380, analyzerId, "LS"),
                BuildDefinition("PRL", "Prolactin", "Prolactin", "Prolactin", "Hormones", "ng/mL", 4m, 23m, 1390, analyzerId, "LS"),
                BuildDefinition("AMH", "Anti-Mullerian Hormone", "Anti-Mullerian Hormone", "AMH", "Hormones", "ng/mL", 1m, 4m, 1400, analyzerId, "LS"),

                // Gastric
                BuildDefinition("PGI", "Pepsinogen I", "Pepsinogen I", "PGI", "Gastric", "ng/mL", 30m, 160m, 1510, analyzerId, "LS"),
                BuildDefinition("PGII", "Pepsinogen II", "Pepsinogen II", "PGII", "Gastric", "ng/mL", 3m, 15m, 1520, analyzerId, "LS"),
                BuildDefinition("G17", "Gastrin-17", "Gastrin-17", "G17", "Gastric", "pmol/L", 1m, 10m, 1530, analyzerId, "LS"),

                // Renal
                BuildDefinition("NGAL", "NGAL", "NGAL", "NGAL", "Renal", "ng/mL", 0m, 150m, 1610, analyzerId, "LS"),
                BuildDefinition("MALB", "Microalbumin", "Microalbumin", "MALB", "Renal", "mg/L", 0m, 30m, 1620, analyzerId, "LS"),
                BuildDefinition("B2MG", "Beta-2 Microglobulin", "Beta-2 Microglobulin", "B2MG", "Renal", "mg/L", 0.7m, 1.8m, 1630, analyzerId, "LS"),
                BuildDefinition("CYSC", "Cystatin C", "Cystatin C", "CYSC", "Renal", "mg/L", 0.6m, 1.2m, 1640, analyzerId, "LS"),

                // Other
                BuildDefinition("PSA", "Prostate Specific Antigen", "Prostate Specific Antigen", "PSA", "Other", "ng/mL", 0m, 4m, 1710, analyzerId, "LS")
            }
            .Select(definition =>
            {
                definition.CategoryId = categories[definition.CategoryKey].Id;
                return definition;
            });
        }

        private static IEnumerable<DeviceTestSeedDefinition> GetEs100cDefinitions(
            IReadOnlyDictionary<string, LabCategory> categories,
            int analyzerId)
        {
            return new[]
            {
                BuildDefinition("PT", "Prothrombin Time", "Prothrombin Time", "PT", "Coagulation", "sec", 11m, 13.5m, 1810, analyzerId, "ES100C"),
                BuildDefinition("APTT", "Activated Partial Thromboplastin Time", "Activated Partial Thromboplastin Time", "APTT", "Coagulation", "sec", 25m, 35m, 1820, analyzerId, "ES100C"),
                BuildDefinition("TT", "Thrombin Time", "Thrombin Time", "TT", "Coagulation", "sec", 14m, 19m, 1830, analyzerId, "ES100C")
            }
            .Select(definition =>
            {
                definition.CategoryId = categories[definition.CategoryKey].Id;
                return definition;
            });
        }

        private static DeviceTestSeedDefinition BuildDefinition(
            string deviceCode,
            string displayNameAr,
            string displayNameEn,
            string printName,
            string categoryKey,
            string unit,
            decimal minValue,
            decimal maxValue,
            int sortOrder,
            int analyzerId,
            string internalCodePrefix)
        {
            return new DeviceTestSeedDefinition
            {
                DeviceCode = deviceCode,
                DisplayNameAr = displayNameAr,
                DisplayNameEn = displayNameEn,
                PrintName = printName,
                CategoryKey = categoryKey,
                Unit = unit,
                MinValue = minValue,
                MaxValue = maxValue,
                SortOrder = sortOrder,
                AnalyzerId = analyzerId,
                InternalCodePrefix = internalCodePrefix
            };
        }

        private static ClinicService BuildMappedDeviceTest(
            string code,
            string arabicName,
            string englishName,
            string printName,
            int categoryId,
            string unit,
            string referenceRange,
            int sortOrder,
            int analyzerId,
            string deviceCode,
            string sampleType,
            string notes)
        {
            return new ClinicService
            {
                Code = code,
                NameAr = arabicName,
                NameEn = englishName,
                Type = ServiceType.LabTest,
                Price = 0m,
                Unit = unit,
                NormalRange = referenceRange,
                ReferenceRange = referenceRange,
                ResultType = LabResultType.Number,
                SourceType = LabTestSourceType.Device,
                IsDeviceMapped = true,
                DeviceCode = deviceCode,
                SortOrder = sortOrder,
                PrintName = printName,
                SampleType = sampleType,
                LabCategoryId = categoryId,
                LabAnalyzerId = analyzerId,
                IsActive = true,
                Notes = notes
            };
        }

        private static string BuildRange(decimal minValue, decimal maxValue)
        {
            return $"{FormatDecimal(minValue)} - {FormatDecimal(maxValue)}";
        }

        private static string FormatDecimal(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string ResolveUniqueCode(string deviceCode, string prefix, HashSet<string> usedCodes)
        {
            if (!usedCodes.Contains(deviceCode))
            {
                return deviceCode;
            }

            var candidate = $"{prefix}-{deviceCode}";
            if (!usedCodes.Contains(candidate))
            {
                return candidate;
            }

            var suffix = 2;
            while (usedCodes.Contains($"{candidate}-{suffix}"))
            {
                suffix++;
            }

            return $"{candidate}-{suffix}";
        }

        private static string BuildMappedPairKey(int analyzerId, string deviceCode)
        {
            return $"{analyzerId}:{deviceCode.Trim().ToUpperInvariant()}";
        }

        private sealed class DeviceTestSeedDefinition
        {
            public string DeviceCode { get; set; } = string.Empty;
            public string DisplayNameAr { get; set; } = string.Empty;
            public string DisplayNameEn { get; set; } = string.Empty;
            public string PrintName { get; set; } = string.Empty;
            public string CategoryKey { get; set; } = string.Empty;
            public int CategoryId { get; set; }
            public string Unit { get; set; } = string.Empty;
            public decimal MinValue { get; set; }
            public decimal MaxValue { get; set; }
            public int SortOrder { get; set; }
            public int AnalyzerId { get; set; }
            public string SampleType { get; set; } = "Serum/Plasma";
            public string Notes { get; set; } = "Seeded from device test catalog.";
            public string InternalCodePrefix { get; set; } = "DEV";
        }
    }
}
