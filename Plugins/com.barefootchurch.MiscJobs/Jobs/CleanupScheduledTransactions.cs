using System;
using System.Collections.Generic;
using System.Linq;

using Rock;
using Rock.Data;
using Rock.Jobs;
using Rock.Model;

namespace com.shepherdchurch.Misc.Jobs
{
    public class CleanupScheduledTransactions : RockJob
    {
        public override void Execute()
        {
            List<string> messages = new List<string>();

            using ( var rockContext = new RockContext() )
            {
                var txnService = new FinancialScheduledTransactionService( rockContext );
                var checkDate = DateTime.Now.AddDays( -7 );
                var txns = txnService
                    .Queryable( "AuthorizedPersonAlias.Person,FinancialGateway" )
                    .Where( t => t.IsActive )
                    .Where( t => !t.NextPaymentDate.HasValue || t.NextPaymentDate.Value < checkDate )
                    .ToList();

                foreach ( var txn in txns )
                {
                    string errorMessage = string.Empty;
                    if ( txnService.GetStatus( txn, out errorMessage ) )
                    {
                        rockContext.SaveChanges();
                    }
                    else
                    {
                        messages.Add( errorMessage );
                    }
                }
            }

            this.Result = string.Join( "\r\n", messages );
        }
    }
}
