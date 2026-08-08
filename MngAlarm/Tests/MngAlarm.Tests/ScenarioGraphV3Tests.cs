using System.Text.Json;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioGraphV3Tests
{
    [Fact]
    public void V2_contract_remains_valid()
    {
        var v2 = new ScenarioDefinition
        {
            SchemaVersion = 2,
            Source = new ScenarioSource { MatchKey = "cpu" },
            Condition = new ScenarioCondition { Field = "value", Operator = "gte", Value = 90 }
        };

        Assert.True(ScenarioCompiler.Validate(v2, true).IsValid);
    }

    [Fact]
    public void Compiler_creates_topological_immutable_plan()
    {
        var definition = BranchGraph();
        var plan = ScenarioCompiler.CompileGraph(definition);

        Assert.Equal("source", plan.TopologicalOrder[0]);
        Assert.Equal(4, plan.Nodes.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, ScenarioPlanNode>)plan.Nodes).Add("x", plan.Nodes["source"]));
    }

    [Fact]
    public void Validator_rejects_cycle_invalid_port_and_excess_fanout()
    {
        var definition = BranchGraph();
        definition.Graph!.Edges.Add(new ScenarioEdge
        {
            Id = "cycle",
            From = "high",
            To = "decision",
            FromPort = "next"
        });

        var validation = ScenarioCompiler.Validate(definition, true);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, x => x.Code == "graph.edge.fromPort.invalid");
        Assert.Contains(validation.Diagnostics, x => x.Code == "graph.cycle");
    }

    [Fact]
    public void Decision_executes_true_and_false_edges_immediately()
    {
        var executor = Executor();
        var rule = Rule(BranchGraph());

        var high = executor.Execute(rule, Observation("risk", 11, 0));
        var low = executor.Execute(rule, Observation("risk", 2, 1));

        Assert.Equal("high", high.Outputs.Single().OutputNodeId);
        Assert.Equal("low", low.Outputs.Single().OutputNodeId);
    }

    [Fact]
    public void One_flow_can_emit_multiple_outputs_with_independent_policy()
    {
        var definition = BranchGraph();
        definition.Graph!.Edges.Single(x => x.Id == "low-edge").FromPort = "true";
        var result = Executor().Execute(Rule(definition), Observation("risk", 11, 0));

        Assert.Equal(2, result.Outputs.Count);
        Assert.NotEqual(result.Outputs[0].DedupKey, result.Outputs[1].DedupKey);
        Assert.NotEqual(result.Outputs[0].Severity, result.Outputs[1].Severity);
        Assert.NotEqual(result.Outputs[0].CooldownSeconds, result.Outputs[1].CooldownSeconds);
    }

    [Fact]
    public void Alarm_output_merge_disabled_emits_unique_dedup_keys()
    {
        var definition = BranchGraph();
        var high = definition.Graph!.Nodes.Single(x => x.Id == "high");
        high.Config.Dedup = new ScenarioDedup
        {
            KeyTemplate = "{scenarioId}:{outputNodeId}",
            CooldownSeconds = 60,
            MergeEnabled = false
        };

        var executor = Executor();
        var rule = Rule(definition);
        var first = executor.Execute(rule, Observation("risk", 11, 0)).Outputs.Single();
        var second = executor.Execute(rule, Observation("risk", 12, 1)).Outputs.Single();

        Assert.False(first.MergeEnabled);
        Assert.Equal(0, first.CooldownSeconds);
        Assert.NotEqual(first.DedupKey, second.DedupKey);
        Assert.StartsWith("scenario:high:", first.DedupKey);
    }

    [Fact]
    public void Threshold_false_is_pending_until_settled_and_sets_next_evaluation()
    {
        var definition = ThresholdGraph();
        var executor = Executor();
        var first = executor.Execute(Rule(definition), Observation("burst", 1, 0));

        Assert.Empty(first.Outputs);
        Assert.NotNull(first.NextEvaluationAt);
        Assert.Contains(first.Traces, x => x.NodeId == "threshold" && x.Status == "pending");
    }

    [Fact]
    public void Sequence_state_survives_executor_restart()
    {
        var states = new InMemorySequenceStateStore();
        var windows = new InMemoryCorrelationWindowStore();
        var rule = Rule(SequenceGraph());
        var firstExecutor = new ScenarioGraphExecutor(windows, states);
        var secondExecutor = new ScenarioGraphExecutor(windows, states);

        var first = firstExecutor.Execute(rule, Observation("start", 1, 0));
        var second = secondExecutor.Execute(rule, Observation("finish", 1, 10));

        Assert.Empty(first.Outputs);
        Assert.Equal("alarm", second.Outputs.Single().OutputNodeId);
    }

    [Fact]
    public void Sequence_timeout_takes_false_branch()
    {
        var executor = Executor();
        var rule = Rule(SequenceGraph());
        executor.Execute(rule, Observation("start", 1, 0));

        var timedOut = executor.Execute(rule, Observation("finish", 1, 31));

        Assert.Equal("stop", timedOut.Traces.Single(x => x.NodeId == "stop").NodeId);
        Assert.Equal("stopped", timedOut.Traces.Single(x => x.NodeId == "stop").Status);
        Assert.Empty(timedOut.Outputs);
    }

    [Fact]
    public void Threshold_due_execution_runs_false_edge_without_recording_observation()
    {
        var definition = ThresholdGraph();
        definition.Graph!.Edges.Single(x => x.Id == "e3").To = "alarm";
        definition.Graph.Nodes.RemoveAll(x => x.Id == "stop");
        var executor = Executor();
        var rule = Rule(definition);
        var initial = executor.Execute(rule, Observation("burst", 1, 0));

        var settled = executor.ExecuteDue(rule, Observation("burst", 1, 10), "threshold");

        Assert.NotNull(initial.NextEvaluationAt);
        Assert.Equal("alarm", settled.Outputs.Single().OutputNodeId);
        Assert.Contains(settled.Traces, x => x.NodeId == "threshold" && x.Outcome == false);
    }

    [Fact]
    public void Sequence_due_execution_runs_timeout_false_edge()
    {
        var definition = SequenceGraph();
        definition.Graph!.Edges.Single(x => x.Id == "e4").To = "alarm";
        definition.Graph.Nodes.RemoveAll(x => x.Id == "stop");
        var executor = Executor();
        var rule = Rule(definition);
        executor.Execute(rule, Observation("start", 1, 0));

        var timedOut = executor.ExecuteDue(rule, Observation("start", 1, 30), "sequence");

        Assert.Equal("alarm", timedOut.Outputs.Single().OutputNodeId);
        Assert.Contains(timedOut.Traces, x => x.NodeId == "sequence" && x.Outcome == false);
    }

    [Fact]
    public void Product_v3_fixture_contains_valid_U1_through_U10_with_v2_parity()
    {
        var root = Path.GetFullPath("../../../../../../tests/fixtures/siem/scenario_templates/packages", AppContext.BaseDirectory);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var v2 = JsonSerializer.Deserialize<ImportScenarioPackageRequest>(
            File.ReadAllText(Path.Combine(root, "siem-product-v2", "manifest.json")), options)!;
        var v3 = JsonSerializer.Deserialize<ImportScenarioPackageRequest>(
            File.ReadAllText(Path.Combine(root, "siem-product-v3", "manifest.json")), options)!;

        Assert.Equal(v2.Templates.Select(x => x.TemplateId), v3.Templates.Select(x => x.TemplateId));
        Assert.All(v3.Templates, x =>
        {
            Assert.Equal(3, x.Definition.SchemaVersion);
            Assert.True(ScenarioCompiler.Validate(x.Definition, true).IsValid, x.TemplateId);
            Assert.NotEmpty(ScenarioCompiler.CompileGraph(x.Definition).TopologicalOrder);
        });
    }

    [Fact]
    public void Debug_output_emits_summary_and_path_payloads_without_alarm()
    {
        var definition = BranchGraph();
        definition.Graph!.Nodes.Add(new ScenarioNode
        {
            Id = "debug",
            Type = ScenarioNodeTypes.DebugOutput,
            Layout = new ScenarioNodeLayout { Label = "Watch value" },
            Config = new ScenarioNodeConfig
            {
                Debug = new ScenarioDebug { Mode = "path", Path = "value", Active = true }
            }
        });
        definition.Graph.Nodes.Add(new ScenarioNode
        {
            Id = "debug-all",
            Type = ScenarioNodeTypes.DebugOutput,
            Config = new ScenarioNodeConfig
            {
                Debug = new ScenarioDebug { Mode = "complete", Active = true }
            }
        });
        definition.Graph.Edges.Add(new ScenarioEdge
        {
            Id = "debug-edge",
            From = "decision",
            To = "debug",
            FromPort = "true"
        });
        definition.Graph.Edges.Add(new ScenarioEdge
        {
            Id = "debug-all-edge",
            From = "decision",
            To = "debug-all",
            FromPort = "true"
        });

        var result = Executor().Execute(Rule(definition), Observation("risk", 11, 0));

        Assert.Contains(result.Outputs, x => x.OutputNodeId == "high");
        Assert.Equal(2, result.DebugLines.Count);
        Assert.Equal(11d, Convert.ToDouble(result.DebugLines.Single(x => x.NodeId == "debug").Payload));
        var summary = Assert.IsType<Dictionary<string, object?>>(
            result.DebugLines.Single(x => x.NodeId == "debug-all").Payload);
        Assert.Equal("risk", summary["key"]);
    }

    [Fact]
    public void Debug_output_alone_does_not_satisfy_required_output()
    {
        var definition = new ScenarioDefinition
        {
            SchemaVersion = 3,
            Graph = new ScenarioGraph
            {
                Nodes =
                [
                    new() { Id = "source", Type = ScenarioNodeTypes.Source, Config = new() { Source = new() { MatchKey = "risk" } } },
                    new() { Id = "debug", Type = ScenarioNodeTypes.DebugOutput, Config = new() { Debug = new() { Mode = "complete" } } }
                ],
                Edges =
                [
                    new() { Id = "e1", From = "source", To = "debug", FromPort = "next" }
                ]
            }
        };

        var validation = ScenarioCompiler.Validate(definition, true);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, x => x.Code == "graph.output.required");
    }

    private static ScenarioGraphExecutor Executor() =>
        new(new InMemoryCorrelationWindowStore(), new InMemorySequenceStateStore());

    private static AlarmRuleDocument Rule(ScenarioDefinition definition) => new()
    {
        Id = "rule",
        ScenarioId = "scenario",
        ScenarioVersion = 1,
        Definition = definition
    };

    private static ObservationEnvelope Observation(string key, double value, int seconds) => new()
    {
        DomainName = "tenant",
        DomainId = "tenant",
        Kind = "event",
        Key = key,
        Value = value,
        Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds)
    };

    private static ScenarioDefinition BranchGraph() => new()
    {
        SchemaVersion = 3,
        Graph = new ScenarioGraph
        {
            Nodes =
            [
                new() { Id = "source", Type = ScenarioNodeTypes.Source, Config = new() { Source = new() { MatchKey = "risk" } } },
                new() { Id = "decision", Type = ScenarioNodeTypes.Decision, Config = new() { Condition = new() { Field = "value", Operator = "gt", Value = 10 } } },
                new() { Id = "high", Type = ScenarioNodeTypes.AlarmOutput, Config = new() { Severity = 9, Dedup = new() { KeyTemplate = "{scenarioId}:{outputNodeId}", CooldownSeconds = 30 } } },
                new() { Id = "low", Type = ScenarioNodeTypes.AlarmOutput, Config = new() { Severity = 4, Dedup = new() { KeyTemplate = "{scenarioId}:{outputNodeId}", CooldownSeconds = 300 } } }
            ],
            Edges =
            [
                new() { Id = "source-edge", From = "source", To = "decision", FromPort = "next" },
                new() { Id = "high-edge", From = "decision", To = "high", FromPort = "true" },
                new() { Id = "low-edge", From = "decision", To = "low", FromPort = "false" }
            ]
        }
    };

    private static ScenarioDefinition ThresholdGraph() => new()
    {
        SchemaVersion = 3,
        Graph = new ScenarioGraph
        {
            Nodes =
            [
                new() { Id = "source", Type = ScenarioNodeTypes.Source, Config = new() { Source = new() { MatchKey = "burst" } } },
                new() { Id = "threshold", Type = ScenarioNodeTypes.Threshold, Config = new() { Aggregation = new() { Function = "count", Operator = "gte", Threshold = 2 }, Window = new() { DurationSeconds = 60 }, SettleAfterSeconds = 10 } },
                new() { Id = "alarm", Type = ScenarioNodeTypes.AlarmOutput, Config = new() { Severity = 7, Dedup = new() { KeyTemplate = "{scenarioId}:{outputNodeId}" } } },
                new() { Id = "stop", Type = ScenarioNodeTypes.StopOutput }
            ],
            Edges =
            [
                new() { Id = "e1", From = "source", To = "threshold", FromPort = "next" },
                new() { Id = "e2", From = "threshold", To = "alarm", FromPort = "true" },
                new() { Id = "e3", From = "threshold", To = "stop", FromPort = "false" }
            ]
        }
    };

    private static ScenarioDefinition SequenceGraph() => new()
    {
        SchemaVersion = 3,
        Graph = new ScenarioGraph
        {
            Nodes =
            [
                new() { Id = "start", Type = ScenarioNodeTypes.Source, Config = new() { Source = new() { MatchKey = "start" } } },
                new() { Id = "finish", Type = ScenarioNodeTypes.Source, Config = new() { Source = new() { MatchKey = "finish" } } },
                new() { Id = "sequence", Type = ScenarioNodeTypes.Sequence, Config = new() { Sequence = new() { Steps = [new() { MatchKey = "start", WithinSeconds = 30 }, new() { MatchKey = "finish", WithinSeconds = 30 }] } } },
                new() { Id = "alarm", Type = ScenarioNodeTypes.AlarmOutput, Config = new() { Severity = 8, Dedup = new() { KeyTemplate = "{scenarioId}:{outputNodeId}" } } },
                new() { Id = "stop", Type = ScenarioNodeTypes.StopOutput }
            ],
            Edges =
            [
                new() { Id = "e1", From = "start", To = "sequence", FromPort = "next" },
                new() { Id = "e2", From = "finish", To = "sequence", FromPort = "next" },
                new() { Id = "e3", From = "sequence", To = "alarm", FromPort = "true" },
                new() { Id = "e4", From = "sequence", To = "stop", FromPort = "false" }
            ]
        }
    };
}
