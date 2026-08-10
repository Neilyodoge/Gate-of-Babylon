using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Edgar.GraphBasedGenerator.Common;
using Edgar.GraphBasedGenerator.Common.Exceptions;
using Edgar.GraphBasedGenerator.Grid2D;
using Edgar.GraphBasedGenerator.Grid2D.Internal;
using Edgar.Unity.Diagnostics;
using UnityEngine;

namespace Edgar.Unity
{
    public class PipelineRunner<TPayload> where TPayload : class
    {
        private bool isGenerating = false;
        
        /// <summary>
        ///     Runs given pipeline items with a given payload.
        /// </summary>
        /// <param name="pipelineTasks"></param>
        /// <param name="payload"></param>
        public void Run(IEnumerable<IPipelineTask<TPayload>> pipelineTasks, TPayload payload, bool runDiagnostics = false)
        {
            var enumerator = GetEnumerator(pipelineTasks, payload, runDiagnostics);
            while (enumerator.MoveNext())
            {
                /* empty */
            }
        }

        public IEnumerator GetEnumerator(IEnumerable<IPipelineTask<TPayload>> pipelineTasks, TPayload payload, bool runDiagnostics = false)
        {
            if (isGenerating)
            {
                Debug.LogError("关卡生成尚未完成时再次调用了生成器，通常表示调用配置有误。常见原因是游戏管理器在 Start/Awake 中手动调用生成，同时生成器仍启用了自动生成。若由代码手动生成，请将生成器的“生成时机”设为“手动”。");
            }
            
            isGenerating = true;
            
            var enumerator = GetEnumeratorNoErrorHandling(pipelineTasks, payload);
            while (true)
            {
                try
                {
                    var hasNext = enumerator.MoveNext();
                    if (!hasNext)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    isGenerating = false;
                    
                    switch (e)
                    {
                        case TimeoutException timeoutException:
                            HandleTimeoutException(timeoutException, payload);
                            break;
                        case NoSuitableShapeForRoomException noSuitableShapeForRoom:
                            throw HandleNoSuitableShapeException(noSuitableShapeForRoom, payload);
                    }

                    throw;
                }

                yield return null;
            }

            isGenerating = false;

            if (runDiagnostics)
            {
                var results = Diagnostics.Diagnostics.Run(payload);
                Diagnostics.Diagnostics.DisplayPerformanceResults(results, true);
            }
        }

        private IEnumerator GetEnumeratorNoErrorHandling(IEnumerable<IPipelineTask<TPayload>> pipelineTasks, TPayload payload)
        {
            foreach (var pipelineItem in pipelineTasks)
            {
                yield return null;
                
                pipelineItem.Payload = payload;
                var enumerator = pipelineItem.Process();
                
                yield return null;
                
                while (enumerator.MoveNext())
                {
                    yield return null;
                }
            }
        }

        private void HandleTimeoutException(TimeoutException exception, TPayload payload)
        {
            var results = Diagnostics.Diagnostics.Run(payload);
            exception.DiagnosticResults = results;
            Diagnostics.Diagnostics.DisplayPerformanceResults(results);
        }

        private Exception HandleNoSuitableShapeException(NoSuitableShapeForRoomException exception, TPayload payload)
        {
            var room = exception.Room as RoomNode<RoomBase>;
            var roomTemplates = exception
                .NeighboringShapes.Cast<RoomTemplateInstanceGrid2D>()
                .Select(x => x.RoomTemplate)
                .ToList();

            var results = Diagnostics.Diagnostics.Run(payload);
            Diagnostics.Diagnostics.DisplayNoSuitableShapeResults(results, room.Room, roomTemplates);

            return new GeneratorException("生成器因错误无法生成关卡，请查看控制台上方的诊断信息。");
        }
    }
}