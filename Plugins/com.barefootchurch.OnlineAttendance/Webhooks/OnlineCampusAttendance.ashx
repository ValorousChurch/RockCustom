<%@ WebHandler Language="C#" Class="OnlineCampusAttendance" %>

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml;
using System.Web;

using Newtonsoft.Json;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Utility;

public class OnlineCampusAttendance : IHttpHandler
{
  /// <summary>
  /// Process the incoming http request. This is the web handler entry point.
  /// </summary>
  /// <param name="context">The context that contains all information about this request.</param>
  public void ProcessRequest( HttpContext context )
  {
    int count = context.Request["count"].AsInteger();
    RockContext rockContext = new RockContext();

    MetricValue metricValue = GetMetricValue( rockContext );

    // MetricValues are read-only unless it is Sunday
    if ( RockDateTime.Today.DayOfWeek == DayOfWeek.Sunday )
    {
      metricValue.YValue += count;
      rockContext.SaveChanges();
    }

    context.Response.ContentType = "application/json";
    context.Response.AddHeader("Access-Control-Allow-Origin", "https://live.barefootchurch.com");
    context.Response.Write( string.Format("{{ \"date\":\"{0:yyyy-MM-dd}\", \"value\":{1:n0} }}", metricValue.MetricValueDateTime, metricValue.YValue ) );
    context.Response.End();
  }

  /// <summary>
  /// Get the online attendance MetricValue for this Sunday.
  /// If one is not found, we will create one with the proper partitions.
  /// </summary>
  /// <param name="rockContext">The rock context.</param>
  /// <returns>
  /// A MetricValue object
  /// </returns>
  private MetricValue GetMetricValue ( RockContext rockContext )
  {
    const int attendanceMetricId = 16;
    const int campusPartitionId = 23;
    const int onlineCampusId = 6;
    const int schedulePartitionId = 24;
    const int sundayServiceScheduleId = 43;
    DateTime sundayDateTime = RockDateTime.Today.AddDays( -6 ).SundayDate();

    MetricValueService metricValueService = new MetricValueService( rockContext );

    MetricValue metricValue = metricValueService.Queryable()
      .Where( v =>
        v.MetricId == attendanceMetricId &&
        v.MetricValueDateTime.HasValue && v.MetricValueDateTime == sundayDateTime &&
        v.MetricValuePartitions.Count == 2 &&
        v.MetricValuePartitions.Any( p => p.MetricPartitionId == campusPartitionId && p.EntityId.HasValue && p.EntityId.Value == onlineCampusId ) &&
        v.MetricValuePartitions.Any( p => p.MetricPartitionId == schedulePartitionId && p.EntityId.HasValue && p.EntityId.Value == sundayServiceScheduleId )
      )
      .FirstOrDefault();

    if( metricValue == null )
    {
        metricValue = new MetricValue();
        metricValue.MetricValueType = MetricValueType.Measure;
        metricValue.MetricId = attendanceMetricId;
        metricValue.MetricValueDateTime = sundayDateTime;
        metricValue.YValue = 0;
        metricValueService.Add( metricValue );

        var campusPartition = new MetricValuePartition();
        campusPartition.MetricPartitionId = campusPartitionId;
        campusPartition.EntityId = onlineCampusId;
        metricValue.MetricValuePartitions.Add( campusPartition );

        var schedulePartition = new MetricValuePartition();
        schedulePartition.MetricPartitionId = schedulePartitionId;
        schedulePartition.EntityId = sundayServiceScheduleId;
        metricValue.MetricValuePartitions.Add( schedulePartition );

        rockContext.SaveChanges();
    }

    return metricValue;
  }

  /// <summary>
  /// These webhooks are not reusable and must only be used once.
  /// </summary>
  public bool IsReusable
  {
      get
      {
          return false;
      }
  }
}