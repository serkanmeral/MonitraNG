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
        /// Add search filter stage ($match) for text search across searchable fields
        /// Searches in main text fields and relation text fields (pre-expansion)
        /// </summary>
        /// <param name="searchTerm">Search term to match in text fields</param>
        /// <param name="relationSearchIds">Dictionary of relation field names to matching IDs (from pre-expansion search)</param>
        public AggregatePipelineBuilder AddSearch(
            string? searchTerm,
            Dictionary<string, List<BsonValue>>? relationSearchIds = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) && (relationSearchIds == null || relationSearchIds.Count == 0))
            {
                return this;
            }

            _logger.LogDebug("Adding search filter for term: {SearchTerm}", searchTerm);

            var orConditions = new BsonArray();

            // 1. Search in main collection text fields
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Escape special regex characters in search term
                var escapedTerm = System.Text.RegularExpressions.Regex.Escape(searchTerm);
                var regexPattern = new BsonRegularExpression(escapedTerm, "i"); // case-insensitive

                var textFields = _schema.fields
                    .Where(f => f.fieldType == "text" && !f.name.StartsWith("__"))
                    .ToList();

                foreach (var field in textFields)
                {
                    orConditions.Add(new BsonDocument(field.name, regexPattern));
                }
            }

            // 2. Search in relation fields using pre-collected IDs
            if (relationSearchIds != null && relationSearchIds.Count > 0)
            {
                var relationFields = _schema.fields
                    .Where(f => f.fieldType == "relation" && !string.IsNullOrEmpty(f.relationDataset))
                    .ToList();

                foreach (var relationField in relationFields)
                {
                    if (relationSearchIds.TryGetValue(relationField.name, out var matchingIds) && matchingIds.Count > 0)
                    {
                        // Add condition: relationField.name IN matchingIds
                        orConditions.Add(new BsonDocument(
                            relationField.name,
                            new BsonDocument("$in", new BsonArray(matchingIds))));
                        
                        _logger.LogDebug(
                            "Added relation field search condition for {FieldName} with {Count} matching IDs",
                            relationField.name, matchingIds.Count);
                    }
                }
            }

            if (orConditions.Count > 0)
            {
                var searchMatch = new BsonDocument("$match", new BsonDocument("$or", orConditions));
                _pipeline.Add(searchMatch);
                _logger.LogDebug("Added search $match stage with {Count} conditions", orConditions.Count);
            }
            else
            {
                _logger.LogWarning("No searchable fields found in schema for dataset {DatasetName}", _schema.name);
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
                    // If lookupStage is null, it means stages were already added (for single fields with unwrap)
                    else if (!field.isArray)
                    {
                        _logger.LogDebug("Added $lookup stage(s) with unwrap for single relation field: {FieldName} -> {RelationDataset}", 
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
                    var stages = BuildPersonLookupStages(field);
                    if (stages != null && stages.Count > 0)
                    {
                        foreach (var stage in stages)
                        {
                            _pipeline.Add(stage);
                        }
                        _logger.LogDebug("Added $lookup stage(s) for persons field: {FieldName} -> @users", field.name);
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
                    var stages = BuildPersonGroupLookupStages(field);
                    if (stages != null && stages.Count > 0)
                    {
                        foreach (var stage in stages)
                        {
                            _pipeline.Add(stage);
                        }
                        _logger.LogDebug("Added $lookup stage(s) for personGroups field: {FieldName} -> @groups", field.name);
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
        /// Build $lookup stage(s) for persons field
        /// Returns list of stages (for single fields, includes unwrap stages)
        /// </summary>
        private List<BsonDocument>? BuildPersonLookupStages(FieldDefinition field)
        {
            var stages = new List<BsonDocument>();
            BsonDocument lookup;

            if (field.isArray)
            {
                // Array persons field - use pipeline with $in
                // Handle null/undefined field values by checking if field exists and is array before using $in
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
                                    ["$and"] = new BsonArray
                                    {
                                        // Check if field exists and is not null
                                        new BsonDocument
                                        {
                                            ["$ne"] = new BsonArray
                                            {
                                                $"$${field.name}",
                                                BsonNull.Value
                                            }
                                        },
                                        // Check if field is array
                                        new BsonDocument
                                        {
                                            ["$isArray"] = $"$${field.name}"
                                        },
                                        // Then check if __dataId is in array (this also ensures array is not empty)
                                        new BsonDocument
                                        {
                                            ["$in"] = new BsonArray
                                            {
                                                "$__dataId",
                                                $"$${field.name}"
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__dataId"] = 1,
                                ["username"] = 1,
                                ["email"] = 1,
                                ["firstName"] = 1,
                                ["lastName"] = 1,
                                ["title"] = 1,
                                ["isActive"] = 1
                            }
                        }
                    },
                    ["as"] = field.name
                };
                
                stages.Add(new BsonDocument("$lookup", lookup));
            }
            else
            {
                // Single person field - use lookup, then unwrap array to single object
                // MongoDB $lookup always returns an array, so we need to unwrap it for single fields
                lookup = new BsonDocument
                {
                    ["from"] = "@users",
                    ["localField"] = field.name,
                    ["foreignField"] = "__dataId",
                    ["as"] = $"{field.name}_lookup",  // Temporary name
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__dataId"] = 1,
                                ["username"] = 1,
                                ["email"] = 1,
                                ["firstName"] = 1,
                                ["lastName"] = 1,
                                ["title"] = 1,
                                ["isActive"] = 1
                            }
                        }
                    }
                };
                
                stages.Add(new BsonDocument("$lookup", lookup));
                
                // Add $addFields stage to unwrap array to single object (or null if not found)
                var unwrapStage = new BsonDocument
                {
                    ["$addFields"] = new BsonDocument
                    {
                        [field.name] = new BsonDocument
                        {
                            ["$arrayElemAt"] = new BsonArray
                            {
                                $"${field.name}_lookup",
                                0
                            }
                        }
                    }
                };
                stages.Add(unwrapStage);
                
                // Add $unset to remove temporary lookup field
                var unsetStage = new BsonDocument
                {
                    ["$unset"] = $"{field.name}_lookup"
                };
                stages.Add(unsetStage);
            }

            return stages;
        }

        /// <summary>
        /// Build $lookup stage(s) for personGroups field
        /// Returns list of stages (for single fields, includes unwrap stages)
        /// </summary>
        private List<BsonDocument>? BuildPersonGroupLookupStages(FieldDefinition field)
        {
            var stages = new List<BsonDocument>();
            BsonDocument lookup;

            if (field.isArray)
            {
                // Array personGroups field - use pipeline with $in
                // Handle null/undefined field values by checking if field exists and is array before using $in
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
                                    ["$and"] = new BsonArray
                                    {
                                        // Check if field exists and is not null
                                        new BsonDocument
                                        {
                                            ["$ne"] = new BsonArray
                                            {
                                                $"$${field.name}",
                                                BsonNull.Value
                                            }
                                        },
                                        // Check if field is array
                                        new BsonDocument
                                        {
                                            ["$isArray"] = $"$${field.name}"
                                        },
                                        // Then check if __dataId is in array (this also ensures array is not empty)
                                        new BsonDocument
                                        {
                                            ["$in"] = new BsonArray
                                            {
                                                "$__dataId",
                                                $"$${field.name}"
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__dataId"] = 1,
                                ["name"] = 1,
                                ["description"] = 1
                            }
                        }
                    },
                    ["as"] = field.name
                };
                
                stages.Add(new BsonDocument("$lookup", lookup));
            }
            else
            {
                // Single personGroup field - use lookup, then unwrap array to single object
                // MongoDB $lookup always returns an array, so we need to unwrap it for single fields
                lookup = new BsonDocument
                {
                    ["from"] = "@groups",
                    ["localField"] = field.name,
                    ["foreignField"] = "__dataId",
                    ["as"] = $"{field.name}_lookup",  // Temporary name
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0,
                                ["__dataId"] = 1,
                                ["name"] = 1,
                                ["description"] = 1
                            }
                        }
                    }
                };
                
                stages.Add(new BsonDocument("$lookup", lookup));
                
                // Add $addFields stage to unwrap array to single object (or null if not found)
                var unwrapStage = new BsonDocument
                {
                    ["$addFields"] = new BsonDocument
                    {
                        [field.name] = new BsonDocument
                        {
                            ["$arrayElemAt"] = new BsonArray
                            {
                                $"${field.name}_lookup",
                                0
                            }
                        }
                    }
                };
                stages.Add(unwrapStage);
                
                // Add $unset to remove temporary lookup field
                var unsetStage = new BsonDocument
                {
                    ["$unset"] = $"{field.name}_lookup"
                };
                stages.Add(unsetStage);
            }

            return stages;
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
                // Handle null/undefined field values by checking if field exists and is array before using $in
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
                                    ["$and"] = new BsonArray
                                    {
                                        // Check if field exists and is array
                                        new BsonDocument
                                        {
                                            ["$ne"] = new BsonArray
                                            {
                                                $"$${field.name}",
                                                BsonNull.Value
                                            }
                                        },
                                        new BsonDocument
                                        {
                                            ["$isArray"] = $"$${field.name}"
                                        },
                                        // Then check if __dataId is in array (this also ensures array is not empty)
                                        new BsonDocument
                                        {
                                            ["$in"] = new BsonArray
                                            {
                                                "$__dataId",
                                                $"$${field.name}"
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0
                            }
                        }
                    },
                    ["as"] = field.name
                };
            }
            else
            {
                // Simple lookup for non-array relations
                // Use pipeline to exclude _id field, then unwrap array to single object
                // MongoDB $lookup always returns an array, so we need to unwrap it for single fields
                lookup = new BsonDocument
                {
                    ["from"] = field.relationDataset,
                    ["localField"] = field.name,
                    ["foreignField"] = "__dataId",
                    ["as"] = $"{field.name}_lookup",  // Temporary name
                    ["pipeline"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["$project"] = new BsonDocument
                            {
                                ["_id"] = 0
                            }
                        }
                    }
                };
            }

            var lookupStage = new BsonDocument("$lookup", lookup);
            
            // For single fields, unwrap the array result to a single object
            if (!field.isArray)
            {
                // Add $addFields stage to unwrap array to single object (or null if not found)
                var unwrapStage = new BsonDocument
                {
                    ["$addFields"] = new BsonDocument
                    {
                        [field.name] = new BsonDocument
                        {
                            ["$arrayElemAt"] = new BsonArray
                            {
                                $"${field.name}_lookup",
                                0
                            }
                        }
                    }
                };
                
                // Add $unset to remove temporary lookup field
                var unsetStage = new BsonDocument
                {
                    ["$unset"] = $"{field.name}_lookup"
                };
                
                // Return lookup + unwrap + unset stages as a list
                // We'll add them separately to the pipeline
                _pipeline.Add(lookupStage);
                _pipeline.Add(unwrapStage);
                _pipeline.Add(unsetStage);
                
                return null; // Return null because we already added stages
            }
            
            return lookupStage;
        }
    }
}

