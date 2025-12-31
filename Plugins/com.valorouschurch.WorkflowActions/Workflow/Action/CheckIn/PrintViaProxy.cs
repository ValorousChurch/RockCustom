using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Rock;
using Rock.Attribute;
using Rock.Bus.Message;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

namespace com.valorouschurch.WorkflowActions.Workflow.Action.CheckIn
{
    /// <summary>
    /// Send the provided ZPL to the selected Printer via the selected Print Proxy.
    /// </summary>
    [ActionCategory( "Valorous > Check-In" )]
    [Description( "Send the provided ZPL to the selected Printer via the selected Print Proxy. **NOTE** This action calls an internal method on the checkin class, and could break at any time." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Print Label via Proxy" )]

    [WorkflowTextOrAttribute( "Printer Device Guid", "Attribute Value",
        Description = "The printer device to print the labels on.",
        IsRequired = true,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.DeviceFieldType" },
        Key = AttributeKey.Printer,
        Order = 1 )]

    [DeviceField( "Proxy Device",
        Description = "Overrides the normal proxy that would be used to print the labels on. Leave blank to use the proxy assigned to the printer.",
        IsRequired = false,
        DeviceTypeGuid = Rock.SystemGuid.DefinedValue.DEVICE_TYPE_CLOUD_PRINT_PROXY,
        Key = AttributeKey.Proxy,
        Order = 2 )]

    [CodeEditorField( "ZPL",
        Description = "The ZPL to send to the printer. (<span class='tip tip-lava'></span>)",
        IsRequired = true,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.Lava,
        EditorHeight = 300,
        Key = AttributeKey.ZPL,
        Order = 3 )]

    [LavaCommandsField( "Enabled Lava Commands", "The Lava commands that should be enabled for this action.", false, order: 4 )]

    public class PrintViaProxy : ActionComponent
    {
        private class AttributeKey
        {
            public const string Printer = "Printer";
            public const string Proxy = "Proxy";
            public const string ZPL = "ZPL";
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

            var printer = DeviceCache.Get( GetAttributeValue( action, AttributeKey.Printer, true ).AsGuid(), rockContext );
            int? proxyId = DeviceCache.Get( GetAttributeValue( action, AttributeKey.Proxy ).AsGuid(), rockContext )?.Id;
            string zpl = GetAttributeValue( action, AttributeKey.ZPL ).ResolveMergeFields( GetMergeFields( action ), GetAttributeValue( action, "EnabledLavaCommands" ) ).Trim();

            // Encode the label data
            UTF8Encoding UTF8EncodingWithoutBom = new UTF8Encoding( false );
            byte[] labelContents = UTF8EncodingWithoutBom.GetBytes( zpl );

            // Make sure we have a printer device
            if ( printer == null )
            {
                errorMessages = new List<string> { "Printer device not found." };
                return true;
            }

            // Make sure the printer has an IP
            if ( printer.IPAddress == null )
            {
                errorMessages = new List<string> { "Printer does not have an IP." };
                return true;
            }

            // Determine the proxy device to use
            if ( ! proxyId.HasValue )
            {
                if ( ! printer.ProxyDeviceId.HasValue  )
                {
                    errorMessages = new List<string> { "Unable to determine proxy device to use." };
                    return true;
                }

                proxyId = printer.ProxyDeviceId.Value;

            }

            action.AddLogEntry( $"Attempting to print to printer {printer.Id} via proxy {proxyId}." );

            // Send the print job
            var task = Task.Run( async () =>
            {
                // Ugly hack to call an internal method
                var method = typeof( CloudPrintLabelMessage ).GetMethod( "RequestAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static );
                var resultTask = method.Invoke( null, new object[] { proxyId.Value, printer.Id, labelContents, new CancellationToken() } );
                await (Task) resultTask;
                var resultObj = resultTask.GetType().GetProperty( "Result" ).GetValue( resultTask, null );
                return resultObj.GetType().GetProperty( "Message" ).GetValue( resultObj, null )?.ToString();
            } );

            // This is not ideal, but is a dependable way to wait for the
            // asynchronous task to complete within this synchronous context.
            while ( !task.IsCompleted )
            {
                Thread.Sleep( 50 );
            }

            // When completed, the task will be in one of the three final states:
            // RanToCompletion, Faulted, or Canceled.
            if ( task.IsFaulted )
            {
                errorMessages = new List<string>
                {
                    "An error occurred while printing labels: " + task.Exception?.GetBaseException().Message
                };

                return true;
            }

            // Empty string == success
            if ( task.Result.IsNotNullOrWhiteSpace() )
            {
                errorMessages = new List<string> { task.Result };
                return true;
            }

            return true;
        }
    }
}