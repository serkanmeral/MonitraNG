using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Builder for MongoDB aggregate pipeline
    /// Creates dynamic pipeline based on query parameters
    /// </summary>
    public class AggregatePipelineBuilder
    {
        private readonly ILogger<AggregatePipelineBuilder> _logger;
        private readonly List<BsonDocument> _pipeline;
        private readonly DatasetSchema _schema;
        private readonly string _databaseName;

        public AggregatePipelineBuilder(
            ILogger<AggregatePipelineBuilder> logger,
            DatasetSchema schema,
            string databaseName)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            _pipeline = new List<BsonDocument>();
        }

        /// <summary>
        /// Add $match stage for filtering
        /// </summary>
        public AggregatePipelineBuilder AddMatch(BsonDocument? matchFilter = null)
        {
            if (matchFilter != null && matchFilter.ElementCount > 0)
            {
                _pipeline.Add(new BsonDocument("$match", matchFilter));
                _logger.LogDebug("Added $match stage to pipeline");
            }

            return this;
        }

        /// <summary>
        /// Add $match stage for single document by __dataId
        /// </summary>
        public AggregatePipelineBuilder AddMatchById(string dataId)
        {
            var match = new BsonDocument("$match", new BsonDocument("__dataId", dataId));
            _pipeline.Add(match);
            _logger.LogDebug("Added $match stage for __dataId: {DataId}", dataId);
            return this;
        }

        /// <summary>
        /// Add $sort stage
        /// </summary>
        public AggregatePipelineBuilder AddSort(BsonDocument? sortDefinition = null)
        {
            if (sortDefinition != null && sortDefinition.ElementCount > 0)
            {
                _pipeline.Add(new BsonDocument("$sort", sortDefinition));
                _logger.LogDebug("Added $sort stage to pipeline");
            }

            return this;
        }

        /// <summary>
        /// Add pagination stages ($skip and $limit)
        /// </summary>
        public AggregatePipelineBuilder AddPagination(int skip = 0, int limit = 50)
        {
            if (skip > 0)
            {
                _pipeline.Add(new BsonDocument("$skip", skip));
                _logger.LogDebug("Added $skip stage: {Skip}", skip);
            }

            if (limit > 0)
            {
                _pipeline.Add(new BsonDocument("$limit", limit));
                _logger.LogDebug("Added $limit stage: {Limit}", limit);
            }

            return this;
        }

        /// <summary>
        /// Add relation expansion stages ($lookup)
        /// </summary>
        public AggregatePipelineBuilder AddRelationExpansion(
            bool expand = true,
            int maxDepth = 2,
            int currentDepth = 0,
            HashSet<string>? visitedDatasets = null)
        {
            if (!expand)
            {
                _logger.LogDebug("Relation expansion disabled, skipping $lookup stages");
                return this;
            }

            if (currentDepth >= maxDepth)
            {
                _logger.LogDebug("Max depth reached ({MaxDepth}), skipping relation expansion", maxDepth);
                return this;
            }

            visitedDatasets ??= new HashSet<string>();
            
            // Circular reference check
            if (visitedDatasets.Contains(_schema.name))
            {
                _logger.LogWarning("Circular reference detected for dataset {DatasetName}, skipping expansion", _schema.name);
                return this;
            }

            visitedDatasets.Add(_schema.name);

            var relationFields = _schema.fields
                .Where(f => f.fieldType == "relation" && !string.IsNullOrEmpty(f.relationDataset))
                .ToList();

            foreach (var field in relationFields)
            {
                try
                {
                    var lookupStage = BuildLookupStage(field, currentDepth, maxDepth, visitedDatasets);
                    if (lookupStage != null)
                    {
                        _pipeline.Add(lookupStage);
                        _logger.LogDebug("Added $lookup stage for relation field: {FieldName} -> {RelationDataset}", 
                            field.name, field.relationDataset);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to build $lookup stage for field {FieldName}", field.name);
                }
            }

            return this;
        }

        /// <summary>
            /// Add $project stage for field selection
            /// </summary>
            public AggregatePipelineBuilder AddProject(
                List<string>? fields = null,
                bool showHistory = false)
            {
                var project = new BsonDocument();

                if (fields != null && fields.Any())
                {
                    // Inclusion projection: specify only fields to include
                    // Always include __dataId
                    project["__dataId"] = 1;

                    // Include specified fields
                    foreach (var field in fields)
                    {
                        project[field] = 1;
                    }

                    // Include __history if showHistory=true
                    if (showHistory)
                    {
                        project["__history"] = 1;
                    }
                }
                else
                {
                    // Exclusion projection: exclude only what we don't want
                    // Exclude _id (MongoDB default)
                    project["_id"] = 0;

                    // Exclude __history if showHistory=false
                    if (!showHistory)
                    {
                        project["__history"] = 0;
                    }
                    // If showHistory=true, don't exclude it (it will be included by default)
                }

                if (project.ElementCount > 0)
                {
                    _pipeline.Add(new BsonDocument("$project", project));
                    _logger.LogDebug("Added $project stage (fields: {FieldCount}, showHistory: {ShowHistory})", 
                        fields?.Count ?? 0, showHistory);
                }

                return this;
            }

        /// <summary>
        /// Add persons/personGroups expansion stages ($lookup from @users and @groups)
        /// </summary>
        public AggregatePipelineBuilder AddPersonExpansion(bool expand = true)
        {
            if (!expand)
            {
                _logger.LogDebug("Person expansion disabled, skipping $lookup stages");
                return this;
            }

            // persons field expansion
            var personFields = _schema.fields
                .Where(f => f.fieldType == "persons")
                .ToList();

            foreach (var field in personFields)
            {
                try
                {
                    var lookupStage = BuildPersonLookupStage(field);
                    if (lookupStage != null)
                    {
                        _pipeline.Add(lookupStage);
                        _logger.LogDebug("Added $lookup stage for persons field: {FieldName} -> @users", field.name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to build $lookup stage for persons field {FieldName}", field.name);
                }
            }

            // personGroups field expansion
            var personGroupFields = _schema.fields
                .Where(f => f.fieldType == "personGroups")
                .ToList();

            foreach (var field in personGroupFields)
            {
                try
                {
                    var lookupStage = BuildPersonGroupLookupStage(field);
                    if (lookupStage != null)
                    {
                        _pipeline.Add(lookupStage);
                        _logger.LogDebug("Added $lookup stage for personGroups field: {FieldName} -> @groups", field.name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to build $lookup stage for personGroups field {FieldName}", field.name);
                }
            }

            return this;
        }

        /// <summary>
        /// Build $lookup stage for persons field
        /// </summary>
        private BsonDocument? BuildPersonLookupStage(FieldDefinition field)
        {
            BsonDocument lookup;

            if (field.isArray)
            {
                // Array persons field - use pipeline with $in
                lookup = new BsonDocument
                {
                    ["from"] = "@users",
                    ["let"] = new BsonDocument(field.name, $"${field.name}"),
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$match"] = new BsonDocument
                            {
                                ["$expr"] = new BsonDocument
                                {
                                    ["$in"] = new BsonArray
                                    {
                                        "$__dataId",
                                        $"$${field.name}"
                                    }
                                },
                                ["__isDeleted"] = new BsonDocument("$ne", true)
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__isDeleted"] = 0,
                                ["__history"] = 0
                            }
                        }
                    },
                    ["as"] = field.name
                };
            }
            else
            {
                // Single person field - use simple lookup with pipeline for soft delete filter
                lookup = new BsonDocument
                {
                    ["from"] = "@users",
                    ["localField"] = field.name,
                    ["foreignField"] = "__dataId",
                    ["as"] = field.name,
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$match"] = new BsonDocument
                            {
                                ["__isDeleted"] = new BsonDocument("$ne", true)
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__isDeleted"] = 0,
                                ["__history"] = 0
                            }
                        }
                    }
                };
            }

            return new BsonDocument("$lookup", lookup);
        }

        /// <summary>
        /// Build $lookup stage for personGroups field
        /// </summary>
        private BsonDocument? BuildPersonGroupLookupStage(FieldDefinition field)
        {
            BsonDocument lookup;

            if (field.isArray)
            {
                // Array personGroups field - use pipeline with $in
                lookup = new BsonDocument
                {
                    ["from"] = "@groups",
                    ["let"] = new BsonDocument(field.name, $"${field.name}"),
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$match"] = new BsonDocument
                            {
                                ["$expr"] = new BsonDocument
                                {
                                    ["$in"] = new BsonArray
                                    {
                                        "$__dataId",
                                        $"$${field.name}"
                                    }
                                },
                                ["__isDeleted"] = new BsonDocument("$ne", true)
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__isDeleted"] = 0,
                                ["__history"] = 0
                            }
                        }
                    },
                    ["as"] = field.name
                };
            }
            else
            {
                // Single personGroup field - use simple lookup with pipeline for soft delete filter
                lookup = new BsonDocument
                {
                    ["from"] = "@groups",
                    ["localField"] = field.name,
                    ["foreignField"] = "__dataId",
                    ["as"] = field.name,
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$match"] = new BsonDocument
                            {
                                ["__isDeleted"] = new BsonDocument("$ne", true)
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__isDeleted"] = 0,
                                ["__history"] = 0
                            }
                        }
                    }
                };
            }

            return new BsonDocument("$lookup", lookup);
        }

        /// <summary>
        /// Build and return the pipeline
        /// </summary>
        public List<BsonDocument> Build()
        {
            _logger.LogDebug("Pipeline built with {StageCount} stages", _pipeline.Count);
            return _pipeline;
        }

        /// <summary>
        /// Build $lookup stage for a relation field
        /// </summary>
        private BsonDocument? BuildLookupStage(
            FieldDefinition field,
            int currentDepth,
            int maxDepth,
            HashSet<string> visitedDatasets)
        {
            if (string.IsNullOrEmpty(field.relationDataset))
                return null;

            // Check circular reference
            if (visitedDatasets.Contains(field.relationDataset))
            {
                _logger.LogWarning("Circular reference detected: {DatasetName} -> {RelationDataset}", 
                    _schema.name, field.relationDataset);
                return null;
            }

            BsonDocument lookup;

            // For array relations, use pipeline with $in
            if (field.isArray)
            {
                lookup = new BsonDocument
                {
                    ["from"] = field.relationDataset,
                    ["let"] = new BsonDocument(field.name, $"${field.name}"),
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$match"] = new BsonDocument
                            {
                                ["$expr"] = new BsonDocument
                                {
                                    ["$in"] = new BsonArray
                                    {
                                        "$__dataId",
                                        $"$${field.name}"
                                    }
                                }
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__isDeleted"] = 0
                            }
                        }
                    },
                    ["as"] = field.name
                };
            }
            else
            {
                // Simple lookup for non-array relations
                lookup = new BsonDocument
                {
                    ["from"] = field.relationDataset,
                    ["localField"] = field.name,
                    ["foreignField"] = "__dataId",
                    ["as"] = field.name
                };
            }

            return new BsonDocument("$lookup", lookup);
        }
    }
}

