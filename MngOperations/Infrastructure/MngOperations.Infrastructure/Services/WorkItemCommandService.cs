using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.WorkItems;
using MngOperations.Application.Contracts.Workflow;
using MngOperations.Application.Events;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.FieldBehaviors;
using MngOperations.Application.Permissions;
using MngOperations.Application.Pipeline;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Faz 1 komut pipeline iskeleti: metadata → key → persist → activity → event.
/// Permission ve rule engine sonraki adımlarda.
/// </summary>
public class WorkItemCommandService : IWorkItemCommandService
{
    private readonly IRequestContext _requestContext;
    private readonly IMetadataCache _metadataCache;
    private readonly IWorkItemKeyGenerator _keyGenerator;
    private readonly IMngDataGatewayClient _dg;
    private readonly IOcEventPublisher _eventPublisher;
    private readonly IPermissionEvaluator _permissions;
    private readonly IRuleEngine _ruleEngine;
    private readonly IFieldBehaviorResolver _fieldBehaviors;
    private readonly ISlaCalculator _slaCalculator;
    private readonly IWorkItemTimelineService _timelineService;
    private readonly INotificationOrchestrator _notifications;
    private readonly IMngWorkflowClient _workflowClient;
    private readonly ILogger<WorkItemCommandService> _logger;

    private static readonly HashSet<string> PatchForbiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "__dataId", "key", "stateId", "stateFlowId", "workspaceId", "workspaceKey", "origin",
        WorkItemCoreFields.ExtraFieldsKey, "typeId"
    };

    public WorkItemCommandService(
        IRequestContext requestContext,
        IMetadataCache metadataCache,
        IWorkItemKeyGenerator keyGenerator,
        IMngDataGatewayClient dg,
        IOcEventPublisher eventPublisher,
        IPermissionEvaluator permissions,
        IRuleEngine ruleEngine,
        IFieldBehaviorResolver fieldBehaviors,
        ISlaCalculator slaCalculator,
        IWorkItemTimelineService timelineService,
        INotificationOrchestrator notifications,
        IMngWorkflowClient workflowClient,
        ILogger<WorkItemCommandService> logger)
    {
        _requestContext = requestContext;
        _metadataCache = metadataCache;
        _keyGenerator = keyGenerator;
        _dg = dg;
        _eventPublisher = eventPublisher;
        _permissions = permissions;
        _ruleEngine = ruleEngine;
        _fieldBehaviors = fieldBehaviors;
        _slaCalculator = slaCalculator;
        _timelineService = timelineService;
        _notifications = notifications;
        _workflowClient = workflowClient;
        _logger = logger;
    }

    public Task<CreateWorkItemResponse> CreateAsync(
        CreateWorkItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(
            request.WorkspaceId,
            request.TypeId,
            request.Title,
            request.Description,
            request.Fields,
            request.BoardId,
            request.Assignee,
            request.PriorityId,
            origin: null,
            initialTransitionKey: null,
            cancellationToken);

    public async Task<CreateWorkItemResponse> CreateFromOriginAsync(
        CreateFromOriginRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        RequireDomainId();

        WorkItemOriginMapper.Validate(request.Origin);

        var existing = await FindByOriginCorrelationIdAsync(
            request.WorkspaceId,
            request.Origin.CorrelationId,
            request.Origin.SourceType,
            token,
            cancellationToken);

        if (existing != null)
        {
            var existingId = GetDataId(existing);
            var existingKey = GetString(existing, "key") ?? string.Empty;
            var workspace = await _metadataCache.GetWorkspaceAsync(request.WorkspaceId, token, cancellationToken);
            _permissions.EnsureWorkItemView(workspace, existing);

            _logger.LogInformation(
                "From-origin idempotent hit: correlationId={CorrelationId} workItem={WorkItemKey}",
                request.Origin.CorrelationId,
                existingKey);

            return new CreateWorkItemResponse
            {
                Code = "ALREADY_EXISTS",
                WorkItem = MapToDto(existing, existingId, existingKey)
            };
        }

        var originDict = WorkItemOriginMapper.ToDictionary(request.Origin);

        return await CreateCoreAsync(
            request.WorkspaceId,
            request.TypeId,
            request.Title,
            request.Description,
            request.Fields,
            request.BoardId,
            request.Assignee,
            request.PriorityId,
            originDict,
            request.InitialTransitionKey,
            cancellationToken);
    }

    public async Task<WorkItemDto> PatchAsync(
        string workItemId,
        PatchWorkItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var domainId = RequireDomainId();

        var existing = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = GetString(existing, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing on work item.", "Kayıtta workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemUpdate(workspace, existing);

        var patchKeys = CollectPatchFieldKeys(request);
        if (patchKeys.Count > 0)
        {
            var form = await _metadataCache.ResolveDefaultFormAsync(workspaceId, token, cancellationToken);
            var behaviorContext = new FieldBehaviorResolveContext
            {
                Screen = FieldBehaviorScreen.Form,
                Mode = "edit",
                Workspace = workspace,
                WorkItem = existing,
                Form = form,
                StateId = GetString(existing, "stateId"),
                CanEdit = _permissions.CanEditWorkItem(workspace, existing),
                RuleTrigger = RuleTriggers.WorkItemUpdated
            };
            var behaviors = await _fieldBehaviors.ResolveAllAsync(behaviorContext, cancellationToken);
            _fieldBehaviors.EnsureWritableFields(behaviorContext, behaviors, patchKeys);
        }

        var merged = new Dictionary<string, object?>(existing, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(request.Title))
            merged["title"] = request.Title.Trim();

        // Nullable core alanlar: alan gövdede VARSA (absent değilse) atanır; explicit null/boş ise temizlenir.
        if (TryReadPatchScalar(request.Description, out var description))
            merged["description"] = description;

        if (TryReadPatchScalar(request.Assignee, out var assignee))
            merged["assignee"] = assignee;

        if (TryReadPatchScalar(request.PriorityId, out var priorityId))
            merged["priorityId"] = priorityId;

        if (TryReadPatchScalar(request.BoardId, out var boardId))
            merged["boardId"] = boardId;

        await ApplyIncomingFieldsAsync(
            merged,
            MergeDynamicFields(request.Fields, PatchForbiddenKeys),
            workspace,
            token,
            cancellationToken);

        await RunMutationRulesAsync(
            merged,
            workspace,
            workspaceId,
            GetString(existing, "typeId"),
            GetString(existing, "boardId"),
            GetString(existing, "stateId"),
            RuleTriggers.WorkItemUpdated,
            workItemId,
            GetString(existing, "key"),
            token,
            cancellationToken);

        merged.Remove("__dataId");

        var updated = await _dg.UpdateAsync(OcDatasets.WorkItems, workItemId, merged, token, cancellationToken);
        var workItemKey = GetString(updated, "key") ?? GetString(existing, "key") ?? workItemId;
        var pipeline = new PipelineContext();
        pipeline.CompletedSteps.Add(PipelineSteps.PersistWorkItem);
        var snapshot = ToWorkItemSnapshot(MapToDto(updated, workItemId, workItemKey));

        // Aktivite için alan bazlı ham diff (kullanıcının dokunduğu alanlar). id→ad çözümü read-time'da (GetTimelineAsync).
        var changeExtra = BuildChangeActivityExtra(existing, updated, patchKeys);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.PersistActivity,
            () => WriteActivityAsync(workItemId, workItemKey, "WorkItemUpdated", $"Work item {workItemKey} updated", token, cancellationToken, extra: changeExtra, throwOnFailure: true),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.AutomationRules,
            () => ExecuteAutomationSideEffectsAsync(
                updated,
                workspaceId,
                GetString(updated, "typeId"),
                GetString(updated, "boardId"),
                GetString(updated, "stateId"),
                RuleTriggers.WorkItemUpdated,
                workItemId,
                workItemKey,
                token,
                cancellationToken),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.PublishRabbitMq,
            () => PublishEventAsync(domainId, "updated", workspaceId, workItemId, workItemKey, cancellationToken, throwOnFailure: true),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.DispatchNotifications,
            () => DispatchWorkItemNotificationsAsync(
                RuleTriggers.WorkItemUpdated,
                workspaceId,
                workItemId,
                workItemKey,
                updated,
                token,
                cancellationToken),
            snapshot);

        // Yeniden atamada yerleşik bildirim (yeni atanan değiştiyse; best-effort).
        await _notifications.DispatchAssignmentAsync(
            workItemId,
            workItemKey,
            GetString(updated, "assignee"),
            GetString(existing, "assignee"),
            _requestContext.MngPersonId,
            token,
            cancellationToken);

        return MapToDto(updated, workItemId, workItemKey);
    }

    public async Task<TransitionWorkItemResponse> ApplyTransitionAsync(
        string workItemId,
        string transitionKey,
        TransitionWorkItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var domainId = RequireDomainId();

        if (string.IsNullOrWhiteSpace(transitionKey))
            throw new OperationCoreException("VALIDATION_ERROR", "transitionKey is required.", "transitionKey zorunludur.", 400);

        var existing = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = GetString(existing, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing on work item.", "Kayıtta workspaceId yok.", 500);

        var currentStateId = GetString(existing, "stateId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "stateId missing on work item.", "Kayıtta stateId yok.", 500);

        var stateFlowId = GetString(existing, "stateFlowId");
        if (string.IsNullOrEmpty(stateFlowId))
            throw new OperationCoreException("STATE_FLOW_NOT_CONFIGURED", "Work item has no stateFlowId.", "Kayıtta stateFlowId yok.", 400);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        var stateFlow = await _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);

        var transition = StateFlowCatalog.FindTransition(stateFlow.Transitions, transitionKey, currentStateId);
        if (transition is not { } transitionElement)
        {
            throw new OperationCoreException(
                "TRANSITION_NOT_FOUND",
                $"Transition '{transitionKey}' not found for current state.",
                $"Transition '{transitionKey}' mevcut state için bulunamadı.",
                404);
        }

        StateFlowCatalog.EnsureTransitionValid(transitionElement, transitionKey, currentStateId);
        _permissions.EnsureTransition(workspace, transitionElement, existing);

        var toStateId = StateFlowCatalog.GetToStateId(transitionElement);
        if (string.IsNullOrEmpty(toStateId))
            throw new OperationCoreException("TRANSITION_INVALID", "Transition has no toStateId.", "Transition toStateId içermiyor.", 400);

        var merged = new Dictionary<string, object?>(existing, StringComparer.OrdinalIgnoreCase);
        await ApplyIncomingFieldsAsync(
            merged,
            MergeDynamicFields(request.Fields, PatchForbiddenKeys),
            workspace,
            token,
            cancellationToken);

        StateFlowCatalog.EnsureRequiredFields(transitionElement, merged);

        await RunTransitionRulesAsync(
            merged,
            workspace,
            workspaceId,
            GetString(existing, "typeId"),
            GetString(existing, "boardId"),
            transitionKey,
            currentStateId,
            toStateId,
            workItemId,
            GetString(existing, "key") ?? workItemId,
            token,
            cancellationToken);

        var now = DateTime.UtcNow;
        merged["stateId"] = toStateId;
        merged["lastStateChangeAt"] = now;
        merged.Remove("__dataId");

        await ApplyTerminalStateFieldsAsync(merged, existing, toStateId, now, token, cancellationToken);
        await _slaCalculator.ApplyOnTransitionAsync(merged, existing, now, token, cancellationToken);

        var updated = await _dg.UpdateAsync(OcDatasets.WorkItems, workItemId, merged, token, cancellationToken);
        var wiKey = GetString(updated, "key") ?? GetString(existing, "key") ?? workItemId;

        var pipeline = new PipelineContext();
        pipeline.CompletedSteps.Add(PipelineSteps.PersistWorkItem);
        var snapshot = ToWorkItemSnapshot(MapToDto(updated, workItemId, wiKey));

        // Aktivite changes[]: önce durum geçişi (stateId), ardından geçişte düzenlenen dinamik alanlar.
        var transitionChanges = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["field"] = "stateId",
                ["from"] = currentStateId,
                ["to"] = toStateId
            }
        };
        AppendFieldChanges(transitionChanges, existing, updated, CollectDynamicFieldKeys(request.Fields));
        var transitionExtra = new Dictionary<string, object?>
        {
            ["transitionKey"] = transitionKey,
            ["fromStateId"] = currentStateId,
            ["toStateId"] = toStateId,
            ["changes"] = transitionChanges
        };

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.PersistActivity,
            () => WriteActivityAsync(
                workItemId,
                wiKey,
                "WorkItemTransitioned",
                $"Transition {transitionKey}: {currentStateId} → {toStateId}",
                token,
                cancellationToken,
                transitionExtra,
                throwOnFailure: true),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.PersistTimelineSegment,
            () => _timelineService.RecordTransitionAsync(
                workItemId,
                currentStateId,
                toStateId,
                transitionKey,
                now,
                GetString(updated, "assignee") ?? GetString(existing, "assignee"),
                token,
                cancellationToken,
                throwOnFailure: true),
            snapshot);

        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            await RunPipelineSideEffectAsync(
                pipeline,
                PipelineSteps.PersistComment,
                () => AddCommentInternalAsync(workItemId, wiKey, request.Comment.Trim(), null, token, cancellationToken),
                snapshot);
        }

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.AutomationRules,
            () => ExecuteAutomationSideEffectsAsync(
                updated,
                workspaceId,
                GetString(updated, "typeId"),
                GetString(updated, "boardId"),
                toStateId,
                RuleTriggers.WorkItemTransitioned,
                workItemId,
                wiKey,
                token,
                cancellationToken,
                transitionKey,
                currentStateId,
                toStateId),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.PublishRabbitMq,
            () => PublishEventAsync(domainId, "transitioned", workspaceId, workItemId, wiKey, cancellationToken, transitionKey, throwOnFailure: true),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.DispatchNotifications,
            () => DispatchWorkItemNotificationsAsync(
                RuleTriggers.WorkItemTransitioned,
                workspaceId,
                workItemId,
                wiKey,
                updated,
                token,
                cancellationToken,
                transitionKey,
                currentStateId,
                toStateId),
            snapshot);

        var available = _permissions
            .GetAvailableTransitions(workspace, stateFlow, toStateId)
            .Select(MapAvailableTransition)
            .ToList();

        return new TransitionWorkItemResponse
        {
            WorkItem = MapToDto(updated, workItemId, wiKey),
            AvailableTransitions = available
        };
    }

    public async Task<CommentDto> AddCommentAsync(
        string workItemId,
        AddCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new OperationCoreException("VALIDATION_ERROR", "body is required.", "body zorunludur.", 400);

        var token = RequireToken();
        RequireDomainId();

        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        if (!_permissions.CanEditWorkItem(workspace, workItem))
        {
            throw new OperationCoreException(
                "WORK_ITEM_FORBIDDEN",
                "You cannot comment on this work item.",
                "Bu iş kaydına yorum yapma yetkiniz yok.",
                403);
        }

        return await AddCommentInternalAsync(
            workItemId,
            GetString(workItem, "key") ?? workItemId,
            request.Body.Trim(),
            request.ParentCommentId,
            token,
            cancellationToken,
            request.Mentions,
            request.Attachments);
    }

    public async Task<CommentDto> UpdateCommentAsync(
        string workItemId,
        string commentId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new OperationCoreException("VALIDATION_ERROR", "body is required.", "body zorunludur.", 400);

        var token = RequireToken();
        RequireDomainId();

        var comment = await LoadOwnCommentAsync(workItemId, commentId, token, cancellationToken);
        var body = request.Body.Trim();

        // DG PUT tam değiştirir → mevcut alanları koru, yalnızca gövde + editedDate güncelle.
        // author okuma sırasında tam @users nesnesine genişlemiş olabilir → düz id'ye normalize et.
        var merged = new Dictionary<string, object?>(comment, StringComparer.OrdinalIgnoreCase)
        {
            ["author"] = WorkItemDataHelper.GetPersonRefId(comment, "author"),
            ["body"] = body,
            ["editedDate"] = DateTime.UtcNow
        };
        merged.Remove("__dataId");

        await _dg.UpdateAsync(OcDatasets.Comments, commentId, merged, token, cancellationToken);

        return new CommentDto
        {
            Id = commentId,
            WorkItemId = workItemId,
            Body = body,
            Author = _requestContext.Username,
            ParentCommentId = GetString(comment, "parentCommentId"),
            CommentDate = WorkItemDataHelper.GetDateTime(comment, "commentDate")
        };
    }

    public async Task DeleteCommentAsync(
        string workItemId,
        string commentId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        RequireDomainId();

        await LoadOwnCommentAsync(workItemId, commentId, token, cancellationToken);

        var deleted = await _dg.DeleteAsync(OcDatasets.Comments, commentId, token, cancellationToken);
        if (!deleted)
        {
            throw new OperationCoreException(
                "COMMENT_DELETE_FAILED",
                $"Comment '{commentId}' could not be deleted.",
                $"Yorum '{commentId}' silinemedi.",
                502);
        }
    }

    // Yorumu yükler; iş kaydına ait olduğunu ve geçerli kullanıcının yazarı olduğunu doğrular (aksi 404/403).
    private async Task<Dictionary<string, object?>> LoadOwnCommentAsync(
        string workItemId,
        string commentId,
        string token,
        CancellationToken cancellationToken)
    {
        var comment = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.Comments, commentId, token, cancellationToken);
        if (comment == null)
        {
            throw new OperationCoreException(
                "COMMENT_NOT_FOUND",
                $"Comment '{commentId}' not found.",
                $"Yorum '{commentId}' bulunamadı.",
                404);
        }

        // path manipülasyonuna karşı: yorum gerçekten bu iş kaydına mı ait?
        if (!string.Equals(GetString(comment, "sourceRecordId"), workItemId, StringComparison.Ordinal))
        {
            throw new OperationCoreException(
                "COMMENT_NOT_FOUND",
                $"Comment '{commentId}' does not belong to work item '{workItemId}'.",
                $"Yorum '{commentId}' bu iş kaydına ait değil.",
                404);
        }

        var authorId = WorkItemDataHelper.GetPersonRefId(comment, "author");
        var me = _requestContext.MngPersonId;
        if (string.IsNullOrWhiteSpace(authorId)
            || string.IsNullOrWhiteSpace(me)
            || !string.Equals(authorId, me, StringComparison.Ordinal))
        {
            throw new OperationCoreException(
                "COMMENT_FORBIDDEN",
                "You can only modify your own comments.",
                "Yalnızca kendi yorumlarınızı düzenleyebilir veya silebilirsiniz.",
                403);
        }

        return comment;
    }

    public async Task DeleteAsync(
        string workItemId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var domainId = RequireDomainId();

        var existing = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = GetString(existing, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing on work item.", "Kayıtta workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        // Silme en az düzenleme (manager/edit) yetkisi gerektirir — ayrı delete yetkisi Faz 1'de yok.
        _permissions.EnsureWorkItemUpdate(workspace, existing);

        var workItemKey = GetString(existing, "key") ?? workItemId;

        // İlişki guard'ı: bağlı link (kaynak/hedef) veya alt kayıt (parentItemId) varsa silmeyi engelle.
        // `force=true` ile aşılır (UI "yine de sil" onayı). Linkler ayrıca silinmez — askıda kalmaması için
        // kullanıcı önce ilişkileri çözmeli; force ile silinen kaydın linkleri best-effort temizlenir.
        if (!force)
            await EnsureNoBlockingRelationsAsync(workItemId, workItemKey, token, cancellationToken);

        var deleted = await _dg.DeleteAsync(OcDatasets.WorkItems, workItemId, token, cancellationToken);
        if (!deleted)
        {
            throw new OperationCoreException(
                "WORK_ITEM_DELETE_FAILED",
                $"Work item '{workItemId}' could not be deleted.",
                $"İş kaydı '{workItemId}' silinemedi.",
                502);
        }

        // force ile silindiyse askıda kalmaması için bu kayda ait linkleri best-effort temizle.
        if (force)
        {
            try
            {
                await DeleteRelatedLinksAsync(workItemId, token, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Force delete link cleanup failed for work item {WorkItemId} (non-fatal)", workItemId);
            }
        }

        // Kalıcı silme sonrası yan etkiler best-effort: kayıt zaten yok, hata ana işlemi geri almaz.
        await WriteActivityAsync(
            workItemId,
            workItemKey,
            "WorkItemDeleted",
            $"Work item {workItemKey} deleted",
            token,
            cancellationToken);

        try
        {
            await PublishEventAsync(domainId, "deleted", workspaceId, workItemId, workItemKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Publish deleted event failed for work item {WorkItemId} (non-fatal)", workItemId);
        }

        _logger.LogInformation("Deleted work item {WorkItemKey} ({WorkItemId}) in workspace {WorkspaceId}", workItemKey, workItemId, workspaceId);
    }

    public async Task RunAutomationRulesAsync(
        string workItemId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var existing = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = GetString(existing, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing on work item.", "Kayıtta workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, existing);

        var typeId = GetString(existing, "typeId");
        var boardId = GetString(existing, "boardId");
        var stateId = GetString(existing, "stateId");
        var workItemKey = GetString(existing, "key") ?? workItemId;

        await ExecuteAutomationSideEffectsAsync(
            existing,
            workspaceId,
            typeId,
            boardId,
            stateId,
            trigger,
            workItemId,
            workItemKey,
            token,
            cancellationToken);
    }

    /// <summary>
    /// Silmeden önce bloklayan ilişki kontrolü: bağlı link (kaynak/hedef) veya alt kayıt (parentItemId) varsa
    /// 409 WORK_ITEM_HAS_RELATIONS fırlatır (sayılar details'te). `force` ile bu kontrol atlanır.
    /// </summary>
    private async Task EnsureNoBlockingRelationsAsync(
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken)
    {
        var outgoing = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString($"sourceWorkItemId:eq:{workItemId}")}&limit=50",
            token, cancellationToken);
        var incoming = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString($"targetWorkItemId:eq:{workItemId}")}&limit=50",
            token, cancellationToken);
        var children = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItems,
            $"filter={Uri.EscapeDataString($"parentItemId:eq:{workItemId}")}&limit=50",
            token, cancellationToken);

        var linkCount = outgoing.Count() + incoming.Count();
        var childCount = children.Count();
        if (linkCount == 0 && childCount == 0)
            return;

        throw new OperationCoreException(
            "WORK_ITEM_HAS_RELATIONS",
            $"Work item '{workItemKey}' has {linkCount} link(s) and {childCount} child item(s); resolve relations or force delete.",
            $"'{workItemKey}' kaydının {linkCount} bağlantısı ve {childCount} alt kaydı var; önce ilişkileri çözün ya da yine de silin.",
            409,
            new Dictionary<string, object?> { ["links"] = linkCount, ["children"] = childCount });
    }

    /// <summary>force ile silinen kaydın askıda kalmaması için ilgili linkleri (kaynak/hedef) best-effort siler.</summary>
    private async Task DeleteRelatedLinksAsync(
        string workItemId,
        string token,
        CancellationToken cancellationToken)
    {
        var outgoing = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString($"sourceWorkItemId:eq:{workItemId}")}&limit=200",
            token, cancellationToken);
        var incoming = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString($"targetWorkItemId:eq:{workItemId}")}&limit=200",
            token, cancellationToken);

        var ids = outgoing.Concat(incoming)
            .Select(GetDataId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var id in ids)
            await _dg.DeleteAsync(OcDatasets.Links, id, token, cancellationToken);
    }

    private async Task<CommentDto> AddCommentInternalAsync(
        string workItemId,
        string workItemKey,
        string body,
        string? parentCommentId,
        string token,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? mentions = null,
        IReadOnlyList<CommentAttachmentInput>? attachments = null)
    {
        var now = DateTime.UtcNow;
        var mentionIds = mentions?
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        var payload = new Dictionary<string, object?>
        {
            ["sourceDataset"] = OcDatasets.WorkItems,
            ["sourceRecordId"] = workItemId,
            // Yazar = MngPersonId (Keeper @users id) — createdBy/assignee ile aynı kimlik uzayı;
            // timeline okunurken People diziniyle ada çözülür. (NP-6 ile tutarlı; forward-only.)
            ["author"] = _requestContext.MngPersonId,
            ["body"] = body,
            ["commentDate"] = now
        };

        if (!string.IsNullOrWhiteSpace(parentCommentId))
            payload["parentCommentId"] = parentCommentId;

        if (mentionIds.Count > 0)
            payload["mentions"] = new Dictionary<string, object?> { ["personIds"] = mentionIds };

        // Yorum ekleri: yeni dosyalar base64 `content` ile gönderilir; DG MinIO'ya yükleyip
        // { path, file_name, ... } olarak saklar (op_comments.attachments file isArray).
        var attachmentPayload = attachments?
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Content))
            .Select(a => (object?)new Dictionary<string, object?>
            {
                ["content"] = a.Content,
                ["originalFileName"] = a.OriginalFileName
            })
            .ToList();

        if (attachmentPayload is { Count: > 0 })
            payload["attachments"] = attachmentPayload;

        var persisted = await _dg.CreateAsync(OcDatasets.Comments, payload, token, cancellationToken);
        var commentId = GetDataId(persisted);

        await WriteActivityAsync(
            workItemId,
            workItemKey,
            "CommentAdded",
            $"Comment added on {workItemKey}",
            token,
            cancellationToken);

        if (mentionIds.Count > 0)
        {
            await _notifications.DispatchMentionAsync(
                workItemId,
                workItemKey,
                mentionIds,
                _requestContext.MngPersonId,
                token,
                cancellationToken);
        }

        return new CommentDto
        {
            Id = commentId,
            WorkItemId = workItemId,
            Body = body,
            Author = _requestContext.Username,
            ParentCommentId = parentCommentId,
            CommentDate = now
        };
    }

    private async Task<CreateWorkItemResponse> CreateCoreAsync(
        string workspaceId,
        string typeId,
        string title,
        string? description,
        JsonElement? fields,
        string? boardId,
        string? assignee,
        string? priorityId,
        Dictionary<string, object?>? origin,
        string? initialTransitionKey,
        CancellationToken cancellationToken)
    {
        var token = RequireToken();
        var domainId = RequireDomainId();

        ValidateCore(workspaceId, typeId, title);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkspace(workspace, WorkspaceAction.Create);

        var workItemType = await _metadataCache.GetWorkItemTypeAsync(typeId, token, cancellationToken);

        if (!string.IsNullOrEmpty(workItemType.WorkspaceId)
            && !string.Equals(workItemType.WorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            throw new OperationCoreException(
                "WORK_ITEM_TYPE_WORKSPACE_MISMATCH",
                "Work item type does not belong to the requested workspace.",
                "İş tipi istenen workspace ile eşleşmiyor.",
                400);
        }

        var stateFlowId = workItemType.DefaultStateFlowId ?? workspace.DefaultStateFlowId;
        if (string.IsNullOrEmpty(stateFlowId))
        {
            throw new OperationCoreException(
                "STATE_FLOW_NOT_CONFIGURED",
                "No default state flow on workspace or work item type.",
                "Workspace veya iş tipinde varsayılan state flow tanımlı değil.",
                400);
        }

        var stateFlow = await _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);
        if (string.IsNullOrEmpty(stateFlow.InitialStateId))
        {
            throw new OperationCoreException(
                "INITIAL_STATE_NOT_CONFIGURED",
                "State flow initialStateId is required.",
                "State flow başlangıç state'i tanımlı değil.",
                400);
        }

        var stateId = StateFlowTransitionResolver.ResolveCreateStateId(
            stateFlow,
            initialTransitionKey,
            stateFlow.InitialStateId);

        var key = await _keyGenerator.GenerateNextKeyAsync(workspace, workspaceId, token, cancellationToken);
        var dynamicFields = MergeDynamicFields(fields, PatchForbiddenKeys);

        var payload = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["workspaceKey"] = workspace.Key,
            ["workspaceId"] = workspaceId,
            ["typeId"] = typeId,
            ["category"] = workItemType.Category ?? "default",
            ["title"] = title.Trim(),
            ["stateId"] = stateId,
            ["stateFlowId"] = stateFlowId
        };

        if (!string.IsNullOrWhiteSpace(description))
            payload["description"] = description.Trim();

        if (!string.IsNullOrWhiteSpace(boardId))
            payload["boardId"] = boardId;

        if (!string.IsNullOrWhiteSpace(assignee))
            payload["assignee"] = assignee;

        if (!string.IsNullOrWhiteSpace(priorityId))
            payload["priorityId"] = priorityId;

        if (origin != null)
            payload["origin"] = origin;

        await ApplyIncomingFieldsAsync(payload, dynamicFields, workspace, token, cancellationToken);

        await RunMutationRulesAsync(
            payload,
            workspace,
            workspaceId,
            typeId,
            boardId,
            stateId,
            RuleTriggers.WorkItemCreated,
            workItemId: null,
            workItemKey: key,
            token,
            cancellationToken);

        var now = DateTime.UtcNow;
        payload["lastStateChangeAt"] = now;
        payload.TryAdd("createdAt", now);
        // createdBy = mng_person_id (Keeper @users id) — assignee/watchers ve bildirim aktörüyle aynı
        // kimlik uzayı; aksi halde 'sub' (Keycloak id) yazılır ve person/ad çözümü eşleşmezdi (NP-4 ile
        // aynı sorun). MngPersonId, claim yoksa zaten 'sub'a düşer. Forward-only.
        if (!string.IsNullOrWhiteSpace(_requestContext.MngPersonId))
            payload.TryAdd("createdBy", _requestContext.MngPersonId);

        await _slaCalculator.ApplyOnCreateAsync(
            payload,
            workspaceId,
            typeId,
            priorityId,
            now,
            token,
            cancellationToken);

        Dictionary<string, object?> persisted;
        try
        {
            persisted = await _dg.CreateAsync(OcDatasets.WorkItems, payload, token, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new OperationCoreException(
                "WORK_ITEM_KEY_CONFLICT",
                "Work item key already exists.",
                "WorkItem anahtarı zaten kullanılıyor.",
                409);
        }

        var workItemId = GetDataId(persisted);
        var persistedStateId = GetString(persisted, "stateId") ?? stateId;
        var pipeline = new PipelineContext();
        pipeline.CompletedSteps.Add(PipelineSteps.PersistWorkItem);
        var snapshot = ToWorkItemSnapshot(MapToDto(persisted, workItemId, key));

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.PersistTimelineSegment,
            () => _timelineService.OpenInitialSegmentAsync(
                workItemId,
                persistedStateId,
                now,
                GetString(persisted, "assignee"),
                token,
                cancellationToken,
                throwOnFailure: true),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.AutomationRules,
            () => ExecuteAutomationSideEffectsAsync(
                persisted,
                workspaceId,
                typeId,
                boardId,
                GetString(persisted, "stateId"),
                RuleTriggers.WorkItemCreated,
                workItemId,
                key,
                token,
                cancellationToken),
            snapshot);

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.PersistActivity,
            () => WriteActivityAsync(
                workItemId,
                key,
                origin != null ? "WorkItemCreatedFromOrigin" : "WorkItemCreated",
                origin != null ? $"Work item {key} created from external origin" : $"Work item {key} created",
                token,
                cancellationToken,
                throwOnFailure: true),
            snapshot);

        if (!string.IsNullOrEmpty(_requestContext.DomainName))
        {
            await RunPipelineSideEffectAsync(
                pipeline,
                PipelineSteps.PublishRabbitMq,
                () => PublishEventAsync(domainId, "created", workspaceId, workItemId, key, cancellationToken, throwOnFailure: true),
                snapshot);
        }

        await RunPipelineSideEffectAsync(
            pipeline,
            PipelineSteps.DispatchNotifications,
            () => DispatchWorkItemNotificationsAsync(
                RuleTriggers.WorkItemCreated,
                workspaceId,
                workItemId,
                key,
                persisted,
                token,
                cancellationToken),
            snapshot);

        // Yerleşik atama bildirimi (politikadan bağımsız, best-effort).
        await _notifications.DispatchAssignmentAsync(
            workItemId,
            key,
            GetString(persisted, "assignee"),
            previousAssigneeId: null,
            _requestContext.MngPersonId,
            token,
            cancellationToken);

        _logger.LogInformation(
            "Created work item {WorkItemKey} ({WorkItemId}) in workspace {WorkspaceId}{OriginSuffix}",
            key,
            workItemId,
            workspaceId,
            origin != null ? " (from-origin)" : string.Empty);

        return new CreateWorkItemResponse
        {
            WorkItem = MapToDto(persisted, workItemId, key)
        };
    }

    private async Task<Dictionary<string, object?>?> FindByOriginCorrelationIdAsync(
        string workspaceId,
        string correlationId,
        string sourceType,
        string token,
        CancellationToken cancellationToken)
    {
        var filter =
            $"workspaceId:eq:{workspaceId},origin.correlationId:eq:{correlationId},origin.sourceType:eq:{sourceType}";
        var query = $"filter={Uri.EscapeDataString(filter)}&limit=1&expand=false";

        var matches = await _dg.GetAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, query, token, cancellationToken);
        return matches.FirstOrDefault();
    }

    private async Task<Dictionary<string, object?>> LoadWorkItemAsync(
        string workItemId,
        string token,
        CancellationToken cancellationToken)
    {
        // expand=false: relation alanları ham id kalır. Patch/transition write-back (merged=existing)
        // expand edilmiş labels'ı (op_labels'a expand → düşürülmüş) geri yazıp silmesin (veri kaybı önlenir).
        var item = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, workItemId, token, cancellationToken, expand: false);
        if (item == null)
        {
            throw new OperationCoreException(
                "WORK_ITEM_NOT_FOUND",
                $"Work item '{workItemId}' not found.",
                $"İş kaydı '{workItemId}' bulunamadı.",
                404);
        }

        return item;
    }

    private async Task WriteActivityAsync(
        string workItemId,
        string workItemKey,
        string activityType,
        string summary,
        string token,
        CancellationToken cancellationToken,
        Dictionary<string, object?>? extra = null,
        bool throwOnFailure = false)
    {
        var activity = new Dictionary<string, object?>
        {
            ["sourceDataset"] = OcDatasets.WorkItems,
            ["sourceRecordId"] = workItemId,
            ["activityType"] = activityType,
            ["eventType"] = activityType,
            // Actor = MngPersonId (Keeper @users id) — timeline okunurken People diziniyle ada çözülür.
            ["actor"] = _requestContext.MngPersonId,
            ["message"] = summary,
            ["activityDate"] = DateTime.UtcNow
        };

        if (extra != null)
        {
            foreach (var kv in extra)
                activity[kv.Key] = kv.Value;
        }

        try
        {
            await _dg.CreateAsync(OcDatasets.Activities, activity, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity write failed for work item {WorkItemId} (non-fatal)", workItemId);
            if (throwOnFailure)
                throw;
        }
    }

    /// <summary>
    /// Aktivite extra'sı için alan bazlı ham diff. Değişiklik varsa <c>{ ["changes"] = [...] }</c>, yoksa null döner
    /// (null → WriteActivityAsync ek alan yazmaz, mevcut davranış korunur).
    /// </summary>
    private static Dictionary<string, object?>? BuildChangeActivityExtra(
        IReadOnlyDictionary<string, object?> existing,
        IReadOnlyDictionary<string, object?> updated,
        IReadOnlyCollection<string> keys)
    {
        var changes = new List<Dictionary<string, object?>>();
        AppendFieldChanges(changes, existing, updated, keys);
        if (changes.Count == 0)
            return null;
        return new Dictionary<string, object?> { ["changes"] = changes };
    }

    /// <summary>Belirtilen alanlarda eski/yeni değer farklıysa <c>{ field, from, to }</c> satırı ekler (ham id/scalar).</summary>
    private static void AppendFieldChanges(
        List<Dictionary<string, object?>> changes,
        IReadOnlyDictionary<string, object?> existing,
        IReadOnlyDictionary<string, object?> updated,
        IReadOnlyCollection<string> keys)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                continue;
            // stateId transition'da ayrıca işleniyor; çift satır olmasın.
            if (string.Equals(key, "stateId", StringComparison.OrdinalIgnoreCase) && changes.Count > 0
                && changes.Any(c => string.Equals(c.TryGetValue("field", out var f) ? f as string : null, "stateId", StringComparison.OrdinalIgnoreCase)))
                continue;

            var oldVal = NormalizeChangeValue(WorkItemDataHelper.GetFieldValue(existing, key));
            var newVal = NormalizeChangeValue(WorkItemDataHelper.GetFieldValue(updated, key));
            if (ChangeValuesEqual(oldVal, newVal))
                continue;

            changes.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["field"] = key,
                ["from"] = oldVal,
                ["to"] = newVal
            });
        }
    }

    /// <summary>request.Fields (JSON nesnesi) içindeki alan anahtarları.</summary>
    private static List<string> CollectDynamicFieldKeys(JsonElement? fields)
    {
        var keys = new List<string>();
        if (fields is { ValueKind: JsonValueKind.Object } obj)
        {
            foreach (var prop in obj.EnumerateObject())
                keys.Add(prop.Name);
        }
        return keys;
    }

    /// <summary>
    /// Değeri karşılaştırılabilir/saklanabilir tokena indirger: tek değer → string, çoklu → List&lt;string&gt;, boş → null.
    /// Ref alanlar için id(ler), scalarlar için metin döndürür; ad çözümü read-time'da yapılır.
    /// </summary>
    private static object? NormalizeChangeValue(object? value)
    {
        var tokens = new List<string>();
        CollectChangeTokens(value, tokens);
        return tokens.Count switch
        {
            0 => null,
            1 => tokens[0],
            _ => tokens
        };
    }

    private static void CollectChangeTokens(object? value, List<string> tokens)
    {
        switch (value)
        {
            case null:
                return;
            case JsonElement el:
                CollectChangeTokensFromElement(el, tokens);
                return;
            case string s:
                AddChangeToken(tokens, s);
                return;
            case bool b:
                AddChangeToken(tokens, b ? "true" : "false");
                return;
            case System.Collections.IEnumerable enumerable:
                foreach (var item in enumerable)
                    CollectChangeTokens(item, tokens);
                return;
            default:
                AddChangeToken(tokens, value.ToString());
                return;
        }
    }

    private static readonly string[] ChangeRefIdProps = { "__dataId", "_id", "id" };

    private static void CollectChangeTokensFromElement(JsonElement el, List<string> tokens)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                AddChangeToken(tokens, el.GetString());
                break;
            case JsonValueKind.Number:
                AddChangeToken(tokens, el.ToString());
                break;
            case JsonValueKind.True:
                AddChangeToken(tokens, "true");
                break;
            case JsonValueKind.False:
                AddChangeToken(tokens, "false");
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    CollectChangeTokensFromElement(item, tokens);
                break;
            case JsonValueKind.Object:
                foreach (var n in ChangeRefIdProps)
                {
                    if (el.TryGetProperty(n, out var idEl)
                        && idEl.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(idEl.GetString()))
                    {
                        AddChangeToken(tokens, idEl.GetString());
                        return;
                    }
                }
                break;
        }
    }

    private static void AddChangeToken(List<string> tokens, string? token)
    {
        if (token == null)
            return;
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
            return;
        tokens.Add(trimmed);
    }

    private static bool ChangeValuesEqual(object? a, object? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        if (a is List<string> la && b is List<string> lb)
            return la.SequenceEqual(lb, StringComparer.Ordinal);
        if (a is string sa && b is string sb)
            return string.Equals(sa, sb, StringComparison.Ordinal);
        return false;
    }

    private async Task RunPipelineSideEffectAsync(
        PipelineContext pipeline,
        string step,
        Func<Task> action,
        IReadOnlyDictionary<string, object?>? workItemSnapshot)
    {
        try
        {
            await action();
            pipeline.CompletedSteps.Add(step);
        }
        catch (OperationCoreException ex) when (ex.Code == PipelinePartialFailure.Code)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pipeline partial failure at {FailedStep}. CorrelationId={CorrelationId} CompletedSteps={@CompletedSteps}",
                step,
                pipeline.CorrelationId,
                pipeline.CompletedSteps);

            throw PipelinePartialFailure.Create(step, pipeline, ex, workItemSnapshot);
        }
    }

    private static Dictionary<string, object?> ToWorkItemSnapshot(WorkItemDto dto) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = dto.Id,
            ["key"] = dto.Key,
            ["workspaceId"] = dto.WorkspaceId,
            ["title"] = dto.Title,
            ["stateId"] = dto.StateId,
            ["typeId"] = dto.TypeId,
            ["boardId"] = dto.BoardId,
            ["assignee"] = dto.Assignee
        };

    private async Task ApplyTerminalStateFieldsAsync(
        Dictionary<string, object?> merged,
        IReadOnlyDictionary<string, object?> existing,
        string toStateId,
        DateTime now,
        string token,
        CancellationToken cancellationToken)
    {
        var targetState = await _metadataCache.GetStateAsync(toStateId, token, cancellationToken);

        if (targetState.IsClosed == true)
        {
            merged["closedAt"] = now;
            if (WorkItemDataHelper.GetDateTime(existing, "firstClosedAt") == null)
                merged["firstClosedAt"] = now;
        }
        else if (WorkItemDataHelper.GetDateTime(existing, "closedAt") != null)
        {
            merged["closedAt"] = null;
        }

        if (string.Equals(targetState.Category, "in_progress", StringComparison.OrdinalIgnoreCase)
            && WorkItemDataHelper.GetDateTime(existing, "firstStartedAt") == null)
        {
            merged["firstStartedAt"] = now;
        }

        merged["currentStateDurationMs"] = 0L;
    }

    private Task DispatchWorkItemNotificationsAsync(
        string eventType,
        string workspaceId,
        string workItemId,
        string workItemKey,
        IReadOnlyDictionary<string, object?> workItem,
        string token,
        CancellationToken cancellationToken,
        string? transitionKey = null,
        string? fromStateId = null,
        string? toStateId = null) =>
        _notifications.DispatchWorkItemEventAsync(
            BuildNotificationDispatchRequest(
                eventType,
                workspaceId,
                workItemId,
                workItemKey,
                workItem,
                token,
                transitionKey,
                fromStateId,
                toStateId),
            cancellationToken);

    private NotificationDispatchRequest BuildNotificationDispatchRequest(
        string eventType,
        string workspaceId,
        string workItemId,
        string workItemKey,
        IReadOnlyDictionary<string, object?> workItem,
        string token,
        string? transitionKey = null,
        string? fromStateId = null,
        string? toStateId = null) =>
        new()
        {
            EventType = eventType,
            WorkspaceId = workspaceId,
            WorkItemId = workItemId,
            WorkItemKey = workItemKey,
            WorkItem = workItem,
            TypeId = WorkItemDataHelper.GetPersonRefId(workItem, "typeId")
                ?? WorkItemDataHelper.GetString(workItem, "typeId"),
            BoardId = WorkItemDataHelper.GetPersonRefId(workItem, "boardId")
                ?? WorkItemDataHelper.GetString(workItem, "boardId"),
            TransitionKey = transitionKey,
            FromStateId = fromStateId,
            ToStateId = toStateId,
            // Alıcılar work item alanlarından (assignee/watchers = mng_person_id) çözülür;
            // self-exclude'un çalışması için actor da aynı uzayda olmalı.
            Actor = _requestContext.MngPersonId,
            DomainName = _requestContext.DomainName,
            Token = token
        };

    private async Task PublishEventAsync(
        string domainId,
        string eventType,
        string workspaceId,
        string workItemId,
        string workItemKey,
        CancellationToken cancellationToken,
        string? transitionKey = null,
        bool throwOnFailure = false)
    {
        if (string.IsNullOrEmpty(_requestContext.DomainName))
            return;

        await _eventPublisher.PublishWorkItemEventAsync(new OcWorkItemEvent
        {
            DomainId = domainId,
            DomainName = _requestContext.DomainName,
            EventType = eventType,
            WorkspaceId = workspaceId,
            WorkItemId = workItemId,
            WorkItemKey = workItemKey,
            TransitionKey = transitionKey,
            Actor = _requestContext.Username
        }, cancellationToken, throwOnFailure);
    }

    private async Task RunMutationRulesAsync(
        Dictionary<string, object?> workItem,
        WorkspaceRecord workspace,
        string workspaceId,
        string? typeId,
        string? boardId,
        string? stateId,
        string trigger,
        string? workItemId,
        string? workItemKey,
        string token,
        CancellationToken cancellationToken)
    {
        var context = BuildRuleContext(
            workItem,
            workspaceId,
            trigger,
            typeId,
            boardId,
            stateId,
            workItemId,
            workItemKey);

        await ApplyRulePhaseAsync(workItem, context, RulePhase.PreValidation, workspace, token, cancellationToken);
        await ApplyRulePhaseAsync(workItem, context, RulePhase.Default, workspace, token, cancellationToken);
        await ApplyRulePhaseAsync(workItem, context, RulePhase.PostValidation, workspace, token, cancellationToken);
    }

    private async Task RunTransitionRulesAsync(
        Dictionary<string, object?> workItem,
        WorkspaceRecord workspace,
        string workspaceId,
        string? typeId,
        string? boardId,
        string transitionKey,
        string fromStateId,
        string toStateId,
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken)
    {
        var preContext = BuildRuleContext(
            workItem,
            workspaceId,
            RuleTriggers.WorkItemTransition,
            typeId,
            boardId,
            fromStateId,
            workItemId,
            workItemKey,
            transitionKey,
            fromStateId,
            toStateId);

        await ApplyRulePhaseAsync(workItem, preContext, RulePhase.PreValidation, workspace, token, cancellationToken);

        workItem["stateId"] = toStateId;

        var postContext = BuildRuleContext(
            workItem,
            workspaceId,
            RuleTriggers.WorkItemTransitioned,
            typeId,
            boardId,
            toStateId,
            workItemId,
            workItemKey,
            transitionKey,
            fromStateId,
            toStateId);

        await ApplyRulePhaseAsync(workItem, postContext, RulePhase.Default, workspace, token, cancellationToken);
        await ApplyRulePhaseAsync(workItem, postContext, RulePhase.PostValidation, workspace, token, cancellationToken);
    }

    private async Task ApplyRulePhaseAsync(
        Dictionary<string, object?> workItem,
        RuleExecutionContext context,
        RulePhase phase,
        WorkspaceRecord workspace,
        string token,
        CancellationToken cancellationToken)
    {
        var result = await _ruleEngine.ExecuteAsync(
            context with { WorkItem = workItem },
            phase,
            cancellationToken);

        if (result.HasValidationErrors)
            ThrowRuleValidationErrors(result.ValidationErrors);

        if (result.FieldMutations.Count > 0)
        {
            var catalog = await WorkItemFieldCatalog.BuildEnabledPoolFieldsByKeyAsync(
                workspace, _metadataCache, token, cancellationToken);
            WorkItemFieldWriter.Apply(workItem, result.FieldMutations, catalog);
        }
    }

    private async Task ApplyIncomingFieldsAsync(
        Dictionary<string, object?> target,
        IReadOnlyDictionary<string, object?> incoming,
        WorkspaceRecord workspace,
        string token,
        CancellationToken cancellationToken)
    {
        if (incoming.Count == 0)
            return;

        var catalog = await WorkItemFieldCatalog.BuildEnabledPoolFieldsByKeyAsync(
            workspace, _metadataCache, token, cancellationToken);
        var workspaceId = workspace.DataId ?? string.Empty;

        foreach (var key in incoming.Keys)
        {
            if (WorkItemCoreFields.IsWritable(key) || catalog.ContainsKey(key))
                continue;

            var candidate = await _metadataCache.FindFieldByKeyAsync(key, token, cancellationToken);
            if (candidate != null && WorkItemFieldCatalog.IsPoolFieldForWorkspace(candidate, workspaceId))
            {
                throw new OperationCoreException(
                    "FIELD_NOT_ENABLED",
                    $"Field '{key}' is not enabled for this workspace.",
                    $"'{key}' alanı bu workspace için etkin değil.",
                    400);
            }

            throw new OperationCoreException(
                "UNKNOWN_FIELD",
                $"Field '{key}' is not defined or not allowed.",
                $"'{key}' alanı tanımsız veya kullanılamaz.",
                400);
        }

        WorkItemFieldWriter.Apply(target, incoming, catalog);
    }

    private async Task ExecuteAutomationSideEffectsAsync(
        IReadOnlyDictionary<string, object?> workItem,
        string workspaceId,
        string? typeId,
        string? boardId,
        string? stateId,
        string trigger,
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken,
        string? transitionKey = null,
        string? fromStateId = null,
        string? toStateId = null)
    {
        var context = BuildRuleContext(
            workItem,
            workspaceId,
            trigger,
            typeId,
            boardId,
            stateId,
            workItemId,
            workItemKey,
            transitionKey,
            fromStateId,
            toStateId);

        var result = await _ruleEngine.ExecuteAsync(context, RulePhase.Automation, cancellationToken);

        foreach (var effect in result.SideEffects)
        {
            switch (effect.Type.ToLowerInvariant())
            {
                case "createactivity":
                    var summary = effect.Payload.TryGetValue("summary", out var s) ? s?.ToString() : "Rule automation activity";
                    var activityType = effect.Payload.TryGetValue("activityType", out var at) ? at?.ToString() : "RuleAction";
                    await WriteActivityAsync(
                        workItemId,
                        workItemKey,
                        activityType ?? "RuleAction",
                        summary ?? "Rule automation activity",
                        token,
                        cancellationToken);
                    break;
                case "createnotification":
                case "sendemailviamngnotifiers":
                    await _notifications.DispatchRuleSideEffectAsync(
                        effect.Type,
                        effect.Payload,
                        BuildNotificationDispatchRequest(
                            trigger,
                            workspaceId,
                            workItemId,
                            workItemKey,
                            workItem,
                            token,
                            transitionKey,
                            fromStateId,
                            toStateId),
                        cancellationToken);
                    break;
                case "addwatcher":
                    _logger.LogDebug(
                        "Rule side-effect addWatcher for work item {WorkItemKey} (patch deferred)",
                        workItemKey);
                    break;
                case "startworkflow":
                    await ExecuteStartWorkflowSideEffectAsync(
                        effect.Payload,
                        workItem,
                        workspaceId,
                        typeId,
                        trigger,
                        workItemId,
                        workItemKey,
                        token,
                        cancellationToken);
                    break;
            }
        }

        if (result.FieldMutations.Count > 0)
        {
            try
            {
                var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
                var merged = new Dictionary<string, object?>(workItem, StringComparer.OrdinalIgnoreCase);
                await ApplyIncomingFieldsAsync(merged, result.FieldMutations, workspace, token, cancellationToken);
                merged.Remove("__dataId");
                await _dg.UpdateAsync(OcDatasets.WorkItems, workItemId, merged, token, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Automation field mutation failed for work item {WorkItemId}", workItemId);
            }
        }
    }

    private async Task ExecuteStartWorkflowSideEffectAsync(
        IReadOnlyDictionary<string, object?> payload,
        IReadOnlyDictionary<string, object?> workItem,
        string workspaceId,
        string? typeId,
        string trigger,
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken)
    {
        var workflowId = payload.TryGetValue("workflowId", out var wf) ? wf?.ToString() : null;
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            _logger.LogWarning("startWorkflow side-effect skipped: workflowId missing for work item {WorkItemKey}", workItemKey);
            return;
        }

        var domainName = _requestContext.DomainName;
        if (string.IsNullOrWhiteSpace(domainName))
        {
            _logger.LogWarning("startWorkflow side-effect skipped: domain context missing for work item {WorkItemKey}", workItemKey);
            return;
        }

        var triggerData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["workItemId"] = workItemId,
            ["workItemKey"] = workItemKey,
            ["workspaceId"] = workspaceId,
            ["typeId"] = typeId,
            ["trigger"] = trigger
        };

        if (payload.TryGetValue("triggerData", out var custom) && custom is Dictionary<string, object?> customDict)
        {
            foreach (var (key, value) in customDict)
                triggerData[key] = value;
        }

        var request = new StartWorkflowRunRequest
        {
            WorkflowId = workflowId.Trim(),
            WorkflowVersionId = payload.TryGetValue("workflowVersionId", out var wv) ? wv?.ToString() : null,
            TriggerType = payload.TryGetValue("triggerType", out var tt) && tt != null
                ? tt.ToString() ?? "op_rules"
                : "op_rules",
            TriggerData = triggerData
        };

        try
        {
            var response = await _workflowClient.StartRunAsync(domainName, token, request, cancellationToken);
            _logger.LogInformation(
                "startWorkflow side-effect queued instance={InstanceId} workflow={WorkflowId} workItem={WorkItemKey}",
                response.InstanceId,
                workflowId,
                workItemKey);
        }
        catch (OperationCoreException ex)
        {
            _logger.LogWarning(
                ex,
                "startWorkflow side-effect failed for work item {WorkItemKey} workflow={WorkflowId}",
                workItemKey,
                workflowId);
        }
    }

    private static RuleExecutionContext BuildRuleContext(
        IReadOnlyDictionary<string, object?> workItem,
        string workspaceId,
        string trigger,
        string? typeId,
        string? boardId,
        string? stateId,
        string? workItemId,
        string? workItemKey,
        string? transitionKey = null,
        string? fromStateId = null,
        string? toStateId = null) =>
        new()
        {
            WorkspaceId = workspaceId,
            Trigger = trigger,
            WorkItem = workItem,
            TypeId = typeId ?? GetStringFromDict(workItem, "typeId"),
            BoardId = boardId ?? GetStringFromDict(workItem, "boardId"),
            StateId = stateId ?? GetStringFromDict(workItem, "stateId"),
            WorkItemId = workItemId,
            WorkItemKey = workItemKey,
            TransitionKey = transitionKey,
            FromStateId = fromStateId,
            ToStateId = toStateId
        };

    private static void ThrowRuleValidationErrors(IReadOnlyList<RuleValidationError> errors)
    {
        var primary = errors[0];
        var details = new Dictionary<string, object?>
        {
            ["errors"] = errors.Select(e => new Dictionary<string, object?>
            {
                ["ruleId"] = e.RuleId,
                ["ruleName"] = e.RuleName,
                ["message"] = e.Message,
                ["messageTr"] = e.MessageTr
            }).ToList()
        };

        throw new OperationCoreException(
            "RULE_VALIDATION_FAILED",
            primary.Message,
            primary.MessageTr ?? primary.Message,
            400,
            details);
    }

    private static string? GetStringFromDict(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return null;

        return value switch
        {
            string s => s,
            JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString(),
            _ => value.ToString()
        };
    }

    private static List<string> CollectPatchFieldKeys(PatchWorkItemRequest request)
    {
        var keys = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title))
            keys.Add("title");
        // Gövdede mevcut (absent değil) ise — set veya temizle — yazılabilirlik kontrolüne dahil et.
        if (request.Description.HasValue)
            keys.Add("description");
        if (request.Assignee.HasValue)
            keys.Add("assignee");
        if (request.PriorityId.HasValue)
            keys.Add("priorityId");
        if (request.BoardId.HasValue)
            keys.Add("boardId");

        if (request.Fields is { ValueKind: JsonValueKind.Object } fields)
        {
            foreach (var prop in fields.EnumerateObject())
                keys.Add(prop.Name);
        }

        return keys;
    }

    private static AvailableTransitionDto MapAvailableTransition(JsonElement transition) =>
        new()
        {
            TransitionKey = StateFlowCatalog.GetStringProperty(transition, "transitionKey") ?? string.Empty,
            Label = StateFlowCatalog.GetStringProperty(transition, "label"),
            FromStateId = StateFlowCatalog.GetStringProperty(transition, "fromStateId") ?? string.Empty,
            ToStateId = StateFlowCatalog.GetStringProperty(transition, "toStateId") ?? string.Empty
        };

    private static WorkItemDto MapToDto(Dictionary<string, object?> data, string id, string key)
    {
        return new WorkItemDto
        {
            Id = id,
            Key = GetString(data, "key") ?? key,
            WorkspaceId = GetString(data, "workspaceId") ?? string.Empty,
            WorkspaceKey = GetString(data, "workspaceKey"),
            TypeId = GetString(data, "typeId") ?? string.Empty,
            Title = GetString(data, "title") ?? string.Empty,
            Description = GetString(data, "description"),
            StateId = GetString(data, "stateId") ?? string.Empty,
            StateFlowId = GetString(data, "stateFlowId"),
            Category = GetString(data, "category") ?? "default",
            BoardId = GetString(data, "boardId"),
            Assignee = GetString(data, "assignee"),
            PriorityId = GetString(data, "priorityId"),
            Origin = ExtractObjectField(data, "origin"),
            Fields = ExtractCustomFields(data)
        };
    }

    private static IReadOnlyDictionary<string, object?>? ExtractObjectField(Dictionary<string, object?> data, string name)
    {
        if (!data.TryGetValue(name, out var value) || value == null)
            return null;

        if (value is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            return el.EnumerateObject()
                .ToDictionary(p => p.Name, p => (object?)DeserializeJsonValue(p.Value));
        }

        if (value is Dictionary<string, object?> dict)
            return dict;

        return null;
    }

    private static object? DeserializeJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
        };

    private static IReadOnlyDictionary<string, object?>? ExtractCustomFields(Dictionary<string, object?> data) =>
        WorkItemDataHelper.ReadExtraFields(data);

    private static string GetDataId(Dictionary<string, object?> data)
    {
        var id = GetString(data, "__dataId");
        if (!string.IsNullOrEmpty(id))
            return id;

        throw new OperationCoreException(
            "DG_RESPONSE_INVALID",
            "DataGateway did not return __dataId for created work item.",
            "Oluşturulan kayıt için __dataId dönmedi.",
            502);
    }

    private static string? GetString(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return null;

        return value switch
        {
            string s => s,
            JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// PATCH gövdesindeki nullable scalar alanı tri-state okur:
    /// absent (HasValue==false) → false döner (alan değişmemiş, dokunma);
    /// explicit null veya boş string → true + value=null (temizle);
    /// dolu string → true + trimlenmiş değer.
    /// </summary>
    private static bool TryReadPatchScalar(JsonElement? element, out string? value)
    {
        value = null;
        if (element is not { } el)
            return false;

        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
                value = null;
                return true;
            case JsonValueKind.String:
                var s = el.GetString();
                value = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
                return true;
            default:
                throw new OperationCoreException(
                    "INVALID_FIELDS",
                    "Scalar core field must be a string or null.",
                    "Çekirdek alan bir metin ya da null olmalıdır.",
                    400);
        }
    }

    private static Dictionary<string, object?> MergeDynamicFields(
        JsonElement? fields,
        IReadOnlySet<string>? forbiddenKeys = null)
    {
        forbiddenKeys ??= PatchForbiddenKeys;

        if (fields is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new Dictionary<string, object?>();

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new OperationCoreException(
                "INVALID_FIELDS",
                "fields must be a JSON object.",
                "fields bir JSON nesnesi olmalıdır.",
                400);
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            if (forbiddenKeys.Contains(prop.Name))
            {
                throw new OperationCoreException(
                    "RESERVED_FIELD",
                    $"Field '{prop.Name}' cannot be modified.",
                    $"'{prop.Name}' alanı değiştirilemez.",
                    400);
            }

            result[prop.Name] = JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
        }

        return result;
    }

    private static void ValidateCore(string workspaceId, string typeId, string title)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
            throw new OperationCoreException("VALIDATION_ERROR", "workspaceId is required.", "workspaceId zorunludur.", 400);

        if (string.IsNullOrWhiteSpace(typeId))
            throw new OperationCoreException("VALIDATION_ERROR", "typeId is required.", "typeId zorunludur.", 400);

        if (string.IsNullOrWhiteSpace(title))
            throw new OperationCoreException("VALIDATION_ERROR", "title is required.", "title zorunludur.", 400);
    }

    private string RequireToken()
    {
        if (string.IsNullOrEmpty(_requestContext.BearerToken))
        {
            throw new OperationCoreException(
                "UNAUTHORIZED",
                "Bearer token is required.",
                "Bearer token gerekli.",
                401);
        }

        return _requestContext.BearerToken;
    }

    private string RequireDomainId()
    {
        if (string.IsNullOrEmpty(_requestContext.DomainId))
        {
            throw new OperationCoreException(
                "UNAUTHORIZED",
                "domain_id claim is required.",
                "domain_id claim gerekli.",
                401);
        }

        return _requestContext.DomainId;
    }
}
