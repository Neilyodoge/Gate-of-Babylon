using System.Collections.Generic;
using System.Linq;
using Edgar.GraphBasedGenerator.Grid2D;
using UnityEngine;

namespace Edgar.Unity.Diagnostics
{
    public static class Diagnostics
    {
        public static List<IDiagnosticResult> Run<TPayload>(TPayload payload)
        {
            var results = new List<IDiagnosticResult>();

            if (payload is DungeonGeneratorPayloadGrid2D dungeonGeneratorPayload)
            {
                results.AddRange(Run(dungeonGeneratorPayload.LevelDescription));
                results.Add(new TimeoutLength().Run(dungeonGeneratorPayload.DungeonGenerator));
                results.Add(new MinimumRoomDistance().Run(dungeonGeneratorPayload.DungeonGenerator));
            } 
            // This is added for the 3D version
            else if (payload is DungeonGeneratorPayloadGrid3D payloadGrid3D)
            {
                return GeneratorDiagnosticsGrid3D.Run(payloadGrid3D);
            }

            return results;
        }

        public static List<IDiagnosticResult> Run(LevelDescriptionGrid2D levelDescription)
        {
            var results = new List<IDiagnosticResult>();

            results.Add(new DifferentLengthsOfDoors().Run(levelDescription));
            results.Add(new WrongManualDoors().Run(levelDescription));
            results.Add(new NumberOfCycles().Run(levelDescription));
            results.Add(new NumberOfRooms().Run(levelDescription));
            results.Add(new WrongPositionGameObjects().Run(levelDescription));
            results.Add(new OddCycles().Run(levelDescription));
            results.Add(new CorridorTypes().Run(levelDescription));
            results.Add(new NotEnoughDoors().Run(levelDescription));

            return results;
        }

        public static void DisplayPerformanceResults(List<IDiagnosticResult> results, bool isPreemptive = false)
        {
            var originalLogType = Application.GetStackTraceLogType(LogType.Warning);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

            if (isPreemptive)
            {
                Debug.LogWarning("<size=17><b>--- 性能诊断 ---</b></size>");
                Debug.LogWarning("这是用于分析生成器潜在配置问题的自动诊断流程。");
                Debug.LogWarning($"显示此信息是因为启用了“{nameof(DungeonGeneratorBaseGrid2D.EnableDiagnostics)}”。");
                Debug.LogWarning("若生成性能正常，可以忽略下方建议。");
                Debug.LogWarning($"---");
            }
            else
            {
                Debug.LogWarning("<size=17><b>--- 超时诊断 ---</b></size>");
                Debug.LogWarning("生成器未能在规定时间内生成关卡，通常表示生成器配置存在问题。");
            }

            var problematicResults = results.Where(x => x.IsPotentialProblem).ToList();

            if (problematicResults.Count > 0)
            {
                Debug.LogWarning("以下是对生成器配置潜在问题的自动诊断。");
                Debug.LogWarning("若无法确定处理方式，请在 Edgar 的 GitHub 提交 Issue，并附上下方诊断截图。");

                foreach (var result in problematicResults)
                {
                    if (result.IsPotentialProblem)
                    {
                        PrintResult(result);
                    }
                }
            }
            else
            {
                Debug.LogWarning("自动诊断未发现明确的配置问题。");
                Debug.LogWarning("请在 Edgar 的 GitHub 提交 Issue，以进一步调查生成器性能。");
            }

            Debug.LogWarning("-------- <b>诊断结束</b> --------");

            Application.SetStackTraceLogType(LogType.Warning, originalLogType);
        }

        public static void DisplayNoSuitableShapeResults(List<IDiagnosticResult> results, RoomBase room, List<RoomTemplateGrid2D> roomTemplates)
        {
            var wrongManualDoors = results.SingleOrDefault(x => x is WrongManualDoors.Result && x.IsPotentialProblem);

            var originalLogType = Application.GetStackTraceLogType(LogType.Warning);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

            Debug.LogWarning("<size=17><b>--- 错误诊断 ---</b></size>");
            Debug.LogWarning("一个或多个房间模板存在错误，生成器无法生成关卡。");

            if (wrongManualDoors != null)
            {
                Debug.LogWarning("--");
                Debug.LogWarning($"<b>该错误很可能由手动门模式配置不正确引起，请查看下方“{wrongManualDoors.Name}”诊断章节。</b>");
                Debug.LogWarning("<b>若确认手动门配置无误，请继续阅读。</b>");
                Debug.LogWarning("--");
            }

            Debug.LogWarning($"为房间“{room.GetDisplayName()}”查找合适形状时，没有可与已放置相邻房间连接的模板。");
            Debug.LogWarning($"当时相邻房间使用的模板为：{string.Join(",", roomTemplates.Select(x => $"“{x.Name}”"))}。");
            Debug.LogWarning("请确保在相邻房间模板的每种可能组合下，");
            Debug.LogWarning($"房间“{room.GetDisplayName()}”都至少有一个模板能与其中一个相邻房间连接。");

            if (wrongManualDoors != null)
            {
                PrintResult(wrongManualDoors);
            }

            Debug.LogWarning("-------- <b>诊断结束</b> --------");

            Application.SetStackTraceLogType(LogType.Warning, originalLogType);
        }

        private static void PrintResult(IDiagnosticResult result)
        {
            Debug.LogWarning($"-------- <b>{result.Name}</b> --------");

            foreach (var line in result.Summary.Trim().Split('\n'))
            {
                Debug.LogWarning(line);
            }
        }
    }
}