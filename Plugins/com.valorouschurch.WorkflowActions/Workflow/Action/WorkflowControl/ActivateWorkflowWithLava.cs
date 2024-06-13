// <copyright>
// Copyright Southeast Christian Church
//
// Licensed under the  Southeast Christian Church License (the "License");
// you may not use this file except in compliance with the License.
// A copy of the License should be included with this file.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

/*
Original Source:
  - https://github.com/secc/RockPlugins/blob/5376bd105925e304766a3b9eefa0f9fbbaeff4a7/Plugins/org.secc.Workflow/WorkflowControl/ActivateWorkflowWithLava.cs
*/

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Lava;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

namespace com.valorouschurch.WorkflowActions.Workflow.Action.WorkflowControl
{
    /// <summary>
    /// Activates a new workflow with the provided attribute values.
    /// </summary>
    [ActionCategory( "Valorous > Workflow Control" )]
    [Description( "Activates a new workflow with the provided attribute values." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Activate Workflow with Lava" )]

    [TextField( "Workflow Name",
        Description = "The name of your new workflow. <span class='tip tip-lava'></span>",
        IsRequired = true,
        Key = AttributeKey.WorkflowName,
        Order = 1 )]

    [WorkflowTypeField( "Workflow Type",
        Description = "The workflow type to activate.  To set the Workflow Type from an Attribute, leave this blank and set Workflow Type from Attribute.",
        IsRequired = false,
        Key = AttributeKey.WorkflowType,
        AllowMultiple = false,
        Order = 2 )]

    [WorkflowAttribute( "Workflow Type from Attribute",
        Description = "The workflow type to activate. Either this or Workflow Type must be set.",
        IsRequired = false,
        Key = AttributeKey.WorkflowTypeFromAttribute,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.TextFieldType", "Rock.Field.Types.WorkflowTypeFieldType" },
        Order = 3 )]

    [KeyValueListField( "Workflow Attribute Key",
        Description = "Used to match the current workflow's attribute keys to the keys of the new workflow. The new workflow will inherit the attribute values of the keys provided. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Key = AttributeKey.WorkflowAttributeKey,
        KeyPrompt = "Source Attribute or Lava",
        ValuePrompt = "Target Attribute",
        Order = 4 )]

    [WorkflowAttribute( "Workflow Attribute",
        "The attribute to hold the new activated workflow.",
        IsRequired = false,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.WorkflowFieldType" },
        Order = 5 )]

    public class ActivateWorkflowWithLava : ActionComponent
    {
        private class AttributeKey
        {
            public const string WorkflowName = "WorkflowName";
            public const string WorkflowType = "WorkflowType";
            public const string WorkflowTypeFromAttribute = "WorkflowTypefromAttribute";
            public const string WorkflowAttributeKey = "WorkflowAttributeKey";
            public const string WorkflowAttribute = "WorkflowAttribute";
        }

        /// <summary>
        /// Executes the specified workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        public override bool Execute( RockContext rockContext, WorkflowAction action, object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();
            var workflowTypeGuid = GetAttributeValue( action, AttributeKey.WorkflowType ).AsGuidOrNull();
            var workflowTypeFromAttributeGuid = GetAttributeValue( action, AttributeKey.WorkflowTypeFromAttribute, true ).AsGuidOrNull();
            var workflowName = GetAttributeValue( action, AttributeKey.WorkflowName );

            WorkflowTypeCache workflowType = null;

            if ( workflowTypeGuid.HasValue )
            {
                workflowType = WorkflowTypeCache.Get( workflowTypeGuid.Value );
            }
            else if ( workflowTypeFromAttributeGuid.HasValue )
            {
                workflowType = WorkflowTypeCache.Get( workflowTypeFromAttributeGuid.Value );
            }

            if ( workflowType == null )
            {
                errorMessages.Add( "Workflow type is required" );
                return false;
            }

            if ( string.IsNullOrEmpty( workflowName ) )
            {
                errorMessages.Add( "Workflow name is required" );
                return false;
            }

            if ( !( workflowType.IsActive ?? true ) )
            {
                errorMessages.Add( string.Format( "Workflow type {0} is not active", workflowType ) );
                return true;
            }

            Dictionary<string, string> sourceKeyMap = null;
            var workflowAttributeKeys = GetAttributeValue( action, AttributeKey.WorkflowAttributeKey );
            if ( !string.IsNullOrWhiteSpace( workflowAttributeKeys ) )
            {
                // TODO Find a way upstream to stop an additional being appended to the value
                sourceKeyMap = workflowAttributeKeys.AsDictionaryOrNull();
            }

            sourceKeyMap = sourceKeyMap ?? new Dictionary<string, string>();

            var mergeFields = GetMergeFields( action );
            mergeFields["Entity"] = entity;

            if ( LavaHelper.IsLavaTemplate( workflowName ) )
            {
                workflowName = workflowName.ResolveMergeFields( mergeFields );
            }

            var workflow = Rock.Model.Workflow.Activate( workflowType, workflowName );
            workflow.LoadAttributes( rockContext );
            var newWorkFlowAttr = SetWorkflowAttributeValue( action, AttributeKey.WorkflowAttribute, workflow.Guid );

            foreach ( var keyPair in sourceKeyMap )
            {
                string value = keyPair.Value;
                if ( LavaHelper.IsLavaTemplate( value ) )
                {
                    value = keyPair.Value.ResolveMergeFields( mergeFields );
                }

                // Does the source key exist as an attribute in the source workflow?
                if ( action.Activity.Workflow.Attributes.ContainsKey( keyPair.Key ) )
                {
                    if ( workflow.Attributes.ContainsKey( value ) )
                    {
                        var valueStr = action.Activity.Workflow.AttributeValues[keyPair.Key].Value;
                        workflow.SetAttributeValue( value, valueStr );
                    }
                    else
                    {
                        errorMessages.Add( string.Format( "'{0}' is not an attribute key in the activated workflow: '{1}'", value, workflow.Name ) );
                    }
                }
                else if ( LavaHelper.IsLavaTemplate( keyPair.Key ) )
                {
                    if ( workflow.Attributes.ContainsKey( value ) )
                    {
                        workflow.SetAttributeValue( value, keyPair.Key.ResolveMergeFields( mergeFields ) );
                    }
                    else
                    {
                        errorMessages.Add( string.Format( "'{0}' is not an attribute key in the activated workflow: '{1}'", value, workflow.Name ) );
                    }
                }
                else
                {
                    errorMessages.Add( string.Format( "'{0}' is not an attribute key in this workflow: '{1}'", keyPair.Key, action.Activity.Workflow.Name ) );
                }
            }

            List<string> workflowErrorMessages = new List<string>();
            new WorkflowService( rockContext ).Process( workflow, out workflowErrorMessages );
            errorMessages.AddRange( workflowErrorMessages );

            return true;
        }
    }
}