using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elsa.Scripting.JavaScript.Events;
using Elsa.Services;
using Elsa.Services.Models;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Elsa.Scripting.JavaScript.Handlers
{
    public class RenderJavaScriptTypeDefinitions : INotificationHandler<RenderingTypeScriptDefinitions>
    {
        private readonly IActivityTypeService _activityTypeService;

        public RenderJavaScriptTypeDefinitions(IActivityTypeService activityTypeService)
        {
            _activityTypeService = activityTypeService;
        }

        public async Task Handle(RenderingTypeScriptDefinitions notification, CancellationToken cancellationToken)
        {
            var output = notification.Output;

            output.AppendLine("declare function guid(): string");
            output.AppendLine("declare function parseGuid(text: string): Guid");
            output.AppendLine("declare function setVariable(name: string, value?: any): void;");
            output.AppendLine("declare function getVariable(name: string): any;");
            output.AppendLine("declare function getConfig(section: string): any;");
            output.AppendLine("declare function isNullOrWhiteSpace(text: string): boolean;");
            output.AppendLine("declare function isNullOrEmpty(text: string): boolean;");
            output.AppendLine("declare function getWorkflowDefinitionIdByName(name: string): string;");
            output.AppendLine("declare function getWorkflowDefinitionIdByTag(tag: string): string;");
            output.AppendLine("declare function getActivity(idOrName: string): any;");
            output.AppendLine("declare function getActivityProperty(activityIdOrName: string, propertyName: string): any;");

            output.AppendLine("declare const activityExecutionContext: ActivityExecutionContext;");
            output.AppendLine("declare const workflowExecutionContext: WorkflowExecutionContext;");
            output.AppendLine("declare const workflowInstance: WorkflowInstance;");
            output.AppendLine("declare const workflowInstanceId: string;");
            output.AppendLine("declare const workflowDefinitionId: string;");
            output.AppendLine("declare const workflowDefinitionVersion: number;");
            output.AppendLine("declare const correlationId: string;");
            output.AppendLine("declare const currentCulture: CultureInfo;");
            output.AppendLine("declare const input: any;");

            var workflowDefinition = notification.WorkflowDefinition;

            if (workflowDefinition != null)
            {
                // Workflow Context
                var contextType = workflowDefinition.ContextOptions?.ContextType;

                if (contextType != null)
                {
                    var workflowContextTypeScriptType = notification.GetTypeScriptType(contextType);
                    output.AppendLine($"declare const workflowContext: {workflowContextTypeScriptType}");
                }

                // Workflow Variables.
                foreach (var variable in workflowDefinition.Variables!.Data)
                {
                    var variableType = variable.Value?.GetType() ?? typeof(object);
                    var typeScriptType = notification.GetTypeScriptType(variableType);
                    output.AppendLine($"declare const {variable.Key}: {typeScriptType}");
                }

                // Named Activities.
                var namedActivities = workflowDefinition.Activities.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
                var activityTypeNames = namedActivities.Select(x => x.Type).Distinct().ToList();
                var activityTypes = await Task.WhenAll(activityTypeNames.Select(async activityTypeName => (activityTypeName, await _activityTypeService.GetActivityTypeAsync(activityTypeName, cancellationToken))));
                var activityTypeDictionary = activityTypes.ToDictionary(x => x.activityTypeName, x => x.Item2);

                foreach (var activityType in activityTypeDictionary.Values)
                    await RenderActivityTypeDeclarationAsync(activityType, output);

                output.AppendLine("declare interface Activities {");

                foreach (var activity in namedActivities)
                {
                    var activityType = activityTypeDictionary[activity.Type];
                    var typeScriptType = activityType.TypeName;
                    var targetType = GetActivityTargetType(activity);

                    if (targetType == null)
                        output.AppendLine($"{activity.Name}: {typeScriptType};");
                    else
                        output.AppendLine($"{activity.Name}: {typeScriptType}<{targetType}>;");
                }

                output.AppendLine("}");
                output.AppendLine("declare const activities: Activities");
            }

            async Task RenderActivityTypeDeclarationAsync(ActivityType type, StringBuilder writer)
            {
                var typeName = type.TypeName;
                var descriptor = await type.DescribeAsync();
                var inputProperties = descriptor.InputProperties;
                var outputProperties = descriptor.OutputProperties;

                if (typeName == "HttpEndpoint")
                    writer.AppendLine($"declare interface {typeName}<T> {{");
                else
                    writer.AppendLine($"declare interface {typeName} {{");

                foreach (var property in inputProperties)
                    RenderActivityProperty(writer, typeName, property.Name, property.Type);

                foreach (var property in outputProperties)
                    RenderActivityProperty(writer, typeName, property.Name, property.Type);

                writer.AppendLine("}");
            }

            void RenderActivityProperty(StringBuilder writer, string typeName, string propertyName, Type propertyType)
            {
                var typeScriptType = notification.GetTypeScriptType(propertyType);

                if (typeName == "HttpEndpoint" && propertyName == "Output")
                    writer.AppendLine($"{propertyName}(): {typeScriptType}<T>;");
                else
                    writer.AppendLine($"{propertyName}(): {typeScriptType};");
            }

            string? GetActivityTargetType(Models.ActivityDefinition activity)
            {
                var targetTypeProperty = activity.Properties.FirstOrDefault(x => x.Name == "TargetType");
                if (targetTypeProperty == null) return null;

                var targetTypeValue = targetTypeProperty.Expressions.FirstOrDefault().Value;
                if (targetTypeValue == null) return null;

                var targetType = Type.GetType(targetTypeValue);
                if (targetType == null) return null;

                return targetType.Name;
            }
        }
    }
}