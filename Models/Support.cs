using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace IT_Stat.Models
{
    public class Support
    {
        public static List<Reaction> Reaction(string start, string end, ServiceDesk db)
        {
            List<Reaction> reaction = new List<Reaction>();
            try
            {
                reaction = db.Reaction.FromSqlRaw(
                    @$"with query as 
					(    SELECT t.id, t.userid, t.fio, t.appointdate, t.userdate, 
							sum(t.reactionworktime) AS reactionworktime
						   FROM (SELECT t.id, t.userid, t.fio, t.appointdate, t.userdate, t.workstarttime, t.workendtime, t.diffdate, t.workappointdate, t.workuserdate, 
								CASE
								  WHEN t.workuserdate::date > t.diffdate THEN 
								  CASE
									WHEN t.workappointdate::date = t.diffdate THEN LEAST(t.workendtime - t.workstarttime, t.workendtime - t.workappointdate::time without time zone::interval)
									ELSE t.workendtime - t.workstarttime
								  END
								  ELSE 
								  CASE
									WHEN t.workuserdate::date = t.workappointdate::date THEN t.workuserdate - t.workappointdate
									ELSE (t.workuserdate::time without time zone - t.workstarttime)::interval
								  END
								END AS reactionworktime
							   FROM (SELECT t.id, t.userid, t.fio, t.appointdate, t.userdate, t.workstarttime, t.workendtime, t.diffdate, 
									CASE
									  WHEN t.appointdate::time without time zone::interval > t.workendtime THEN t.appointdate::date + t.workendtime
									  WHEN t.appointdate::time without time zone::interval < t.workstarttime THEN t.appointdate::date + t.workstarttime
									  ELSE t.appointdate
									END AS workappointdate, 
									CASE
									  WHEN t.userdate::time without time zone::interval > t.workendtime THEN t.userdate::date + t.workendtime
									  WHEN t.userdate::time without time zone::interval < t.workstarttime THEN t.userdate::date + t.workstarttime
									  ELSE t.userdate
									END AS workuserdate
								   FROM (SELECT t.id, t.userid, t.fio, t.userdate, t.appointdate, t.diffdate, t.is_holiday, 
										CASE
										  WHEN t.is_holiday = 0 THEN '08:00:00'::interval
										  ELSE '10:00:00'::interval
										END AS workstarttime, 
										CASE
										  WHEN t.is_holiday = 0 THEN '21:00:00'::interval
										  ELSE '19:00:00'::interval
										END AS workendtime
									   FROM (SELECT t.id, t.userid, t.fio, t.userdate, t.appointdate, days.diffdate, 
											CASE
											  WHEN (to_char(days.diffdate, 'D') in ('7', '1')) OR hd.holidaydate IS NOT NULL THEN 1
											  ELSE 0
											END AS is_holiday
										   FROM (SELECT t.id, t.userid, t.fio, t.userdate, 
												CASE
												  WHEN t.groupdate IS NULL OR t.userdate <= t.groupdate THEN t.createdate
												  ELSE t.groupdate
												END AS appointdate
											   FROM (SELECT wo.workorderid AS id, 
													u.userid, 
													u.lastname || ' ' || u.firstname AS fio, 
													from_unixtime(wo.createdtime / 1000) + '03:00:00'::interval AS createdate, 
													from_unixtime(min(woh.operationtime) / 1000) + '03:00:00'::interval AS groupdate, 
													from_unixtime(woh2.operationtime / 1000) + '03:00:00'::interval AS userdate, 
													row_number() OVER (PARTITION BY wo.workorderid ORDER BY woh2.operationtime) AS r
												   FROM workorder wo
												   JOIN workorderstates wos ON wo.workorderid = wos.workorderid
												   JOIN workorder_queue woq ON wo.workorderid = woq.workorderid
												   LEFT JOIN workorderhistory woh ON wo.workorderid = woh.workorderid 
																  AND woh.historyid IN (SELECT diff.historyid
																			   FROM workorderhistorydiff diff
																			   WHERE diff.columnname = 'QUEUEID'
																			   AND diff.current_value in {Startup.group}
																			   )
												   JOIN workorderhistory woh2 ON wo.workorderid = woh2.workorderid 
																  AND woh2.operationownerid in {Startup.specialist}
																  AND woh2.operationownerid <> wo.createdbyid
																  AND NOT (woh2.operationownerid = 1217 and to_char(from_unixtime(woh2.operationtime / 1000), 'YYYY.MM.DD') > '2021.11.07')
												   JOIN sduser u ON woh2.operationownerid = u.userid
												   JOIN categorydefinition cd ON wos.categoryid = cd.categoryid 
																  AND cd.categoryid not in {Startup.category}
												   WHERE wo.createdbyid not in {Startup.specialist}
												   GROUP BY wo.workorderid, u.userid, (u.lastname || ' ') || u.firstname, wo.createdtime, woh2.operationtime) t
											   WHERE t.r = 1) t
										   JOIN (SELECT DISTINCT from_unixtime(wo.createdtime / 1000)::date AS diffdate
											   FROM workorder wo) days ON days.diffdate >= t.appointdate::date AND days.diffdate <= t.userdate::date
										   LEFT JOIN holidaydefinition hd ON days.diffdate = hd.holidaydate
										   WHERE to_char(t.appointdate, 'YYYY.MM.DD') between '{start}' and '{end}'
										) t
									) t
								) t
							) t
						   GROUP BY t.id, t.userid, t.fio, t.appointdate, t.userdate
						   ORDER BY t.id, sum(t.reactionworktime)
						),
						 stat as
						(select id, userid, FIO, AppointDate, UserDate, ReactionWorkTime,
							case when r <= countfree then '0'::interval else ReactionWorkTime end ReactionWorkTimeFree
						   from (select q.id, q.userid, q.FIO, q.AppointDate, q.UserDate, q.ReactionWorkTime,
								row_number() over (partition by q.userid order by q.ReactionWorkTime desc) r,
								round(count(q2.id)::float / 100 * 3) countfree
							   from query q
							   join query q2 on q.userid = q2.userid
							   group by q.id, q.userid, q.FIO, q.AppointDate, q.UserDate, q.ReactionWorkTime
							) t
						)
					select null::int requestid, 'Итого по отделу' FIO, null::timestamp AppointDate, null::timestamp UserDate, 
						   to_char(avg(ReactionWorkTime), 'DD HH24:mi:ss') ReactionWorkTime,
						   to_char(avg(ReactionWorkTimeFree), 'DD HH24:mi:ss') ReactionWorkTimeFree
					  from stat
					union all
					select null::int , FIO, null::timestamp AppointDate, null::timestamp UserDate,
						   to_char(avg(ReactionWorkTime), 'DD HH24:mi:ss'),
						   to_char(avg(ReactionWorkTimeFree), 'DD HH24:mi:ss')
					  from stat
					  group by userid, FIO
					union all
					select id requestid, FIO, AppointDate, UserDate,
						   to_char(ReactionWorkTime, 'DD HH24:mi:ss'),
						   to_char(ReactionWorkTimeFree, 'DD HH24:mi:ss')
					  from stat
                    ").ToList();
            }
            catch (Exception ex)
            {
                Program.logger.Error("Ошибка запроса отчета по времени реакции\n" + ex.Message);
            }
            return reaction;
        }
        public static List<Fact> Fact(string start, string end, ServiceDesk db)
        {
            List<Fact> fact = new List<Fact>();
            try
            {
                fact = db.Fact.FromSqlRaw(@$"with stat as
											(
												select *
												from
												(
												select u.lastname || ' ' || u.firstname AS fio, 'заявка ' || wo.workorderid nodoc, to_char(from_unixtime(wo.resolvedtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD') AS resolvedtime, 
														coalesce(wof.udf_double3::int, 1::int) storypoints,
														case when wos.categoryid in {Startup.category} then 0 else coalesce(wof.udf_double3::int, 1::int) end storypointsrequest,
														case when wos.haslinkedrequest = 't' or linkedworkorderid is not null then coalesce(wof.udf_double3::int, 1::int) else 0 end storypointslinkedrequest,
														case when wos.statusid = 2101 then coalesce(wof.udf_double3::int, 1::int) else 0 end tojira
												from workorder wo
												join workorderstates wos on wo.workorderid = wos.workorderid
												join sduser u on wos.ownerid = u.userid
												left join workorder_fields wof on wo.workorderid = wof.workorderid
												where u.userid in {Startup.specialist}
												and not (u.userid = 1217 and to_char(from_unixtime(wo.resolvedtime / 1000), 'YYYY.MM.DD') > '2021.11.07')
												and wo.resolvedtime <> 0
												union
												select u.lastname || ' ' || u.firstname, 'задача ' || td.taskid, to_char(from_unixtime(td.actualendtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD'), 
														case when td.addtional_cost::int = null or td.addtional_cost::int = 0 then 1 else td.addtional_cost::int end storypoints, 
														0 storypointsrequest, 0 storypointslinkedrequest, 0 tojira
												from taskdetails td
												join sduser u on td.ownerid = u.userid
												where u.userid in {Startup.specialist}
												and not (u.userid = 1217 and to_char(from_unixtime(td.actualendtime / 1000), 'YYYY.MM.DD') > '2021.11.07')
												and td.actualendtime  <> 0
												) t
												where t.resolvedtime between '{start}' and '{end}'
											)
											select ' Итого по отделу' fio, null nodoc, null resolvedtime, sum(storypoints) storypoints, sum(storypointsrequest) storypointsrequest, sum(storypointslinkedrequest) storypointslinkedrequest, sum(tojira) tojira, round(100 * sum(tojira) / sum(storypointsrequest)::numeric, 2) percenttojira
												from stat
											union
											select fio, null, null, sum(storypoints) storypoints, sum(storypointsrequest) storypointsrequest, sum(storypointslinkedrequest) storypointslinkedrequest, sum(tojira) tojira, round(100 * sum(tojira) / sum(storypointsrequest)::numeric, 2) percenttojira
												from stat
												group by fio
											union
											select fio, nodoc, resolvedtime, storypoints, storypointsrequest, storypointslinkedrequest, tojira, null percenttojira
												from stat
											order by 3 desc, 1
                                        ").ToList();
        }
            catch (Exception ex)
            {
                Program.logger.Error("Ошибка запроса отчета по выполненным заявкам\n" + ex.Message);
            }
            return fact;
        }
		public static List<Reaction> Complete(string start, string end, ServiceDesk db)
		{
			List<Reaction> fact = new List<Reaction>();
			try
			{
				fact = db.Reaction.FromSqlRaw(@$"with query as 
												(    SELECT t.id, t.userid, t.fio, t.appointdate, t.userdate, 
														sum(t.reactionworktime) AS reactionworktime
													   FROM (SELECT t.id, t.userid, t.fio, t.appointdate, t.userdate, t.workstarttime, t.workendtime, t.diffdate, t.workappointdate, t.workuserdate, 
															CASE
															  WHEN t.workuserdate::date > t.diffdate THEN 
															  CASE
																WHEN t.workappointdate::date = t.diffdate THEN LEAST(t.workendtime - t.workstarttime, t.workendtime - t.workappointdate::time without time zone::interval)
																ELSE t.workendtime - t.workstarttime
															  END
															  ELSE 
															  CASE
																WHEN t.workuserdate::date = t.workappointdate::date THEN t.workuserdate - t.workappointdate
																ELSE (t.workuserdate::time without time zone - t.workstarttime)::interval
															  END
															END AS reactionworktime
														   FROM (SELECT t.id, t.userid, t.fio, t.appointdate, t.userdate, t.workstarttime, t.workendtime, t.diffdate, 
																CASE
																  WHEN t.appointdate::time without time zone::interval > t.workendtime THEN t.appointdate::date + t.workendtime
																  WHEN t.appointdate::time without time zone::interval < t.workstarttime THEN t.appointdate::date + t.workstarttime
																  ELSE t.appointdate
																END AS workappointdate, 
																CASE
																  WHEN t.userdate::time without time zone::interval > t.workendtime THEN t.userdate::date + t.workendtime
																  WHEN t.userdate::time without time zone::interval < t.workstarttime THEN t.userdate::date + t.workstarttime
																  ELSE t.userdate
																END AS workuserdate
															   FROM (SELECT t.id, t.userid, t.fio, t.userdate, t.appointdate, t.diffdate, t.is_holiday, 
																	CASE
																	  WHEN t.is_holiday = 0 THEN '08:00:00'::interval
																	  ELSE '10:00:00'::interval
																	END AS workstarttime, 
																	CASE
																	  WHEN t.is_holiday = 0 THEN '21:00:00'::interval
																	  ELSE '19:00:00'::interval
																	END AS workendtime
																   FROM (SELECT t.id, t.userid, t.fio, t.userdate, t.appointdate, days.diffdate, 
																		CASE
																		  WHEN (to_char(days.diffdate, 'D') in ('7', '1')) OR hd.holidaydate IS NOT NULL THEN 1
																		  ELSE 0
																		END AS is_holiday
																	   FROM (SELECT t.id, t.userid, t.fio, t.userdate, 
																			CASE
																			  WHEN t.groupdate IS NULL OR t.userdate <= t.groupdate THEN t.createdate
																			  ELSE t.groupdate
																			END AS appointdate
																		   FROM (SELECT wo.workorderid AS id, 
																				u.userid, 
																				u.lastname || ' ' || u.firstname AS fio, 
																				from_unixtime(wo.createdtime / 1000) + '03:00:00'::interval AS createdate, 
																				from_unixtime(min(woh.operationtime) / 1000) + '03:00:00'::interval AS groupdate, 
																				from_unixtime(wo.resolvedtime / 1000) + '03:00:00'::interval AS userdate
																			   FROM workorder wo
																			   JOIN workorderstates wos ON wo.workorderid = wos.workorderid
																							AND wos.ownerid in {Startup.specialist}
																			   JOIN workorder_queue woq ON wo.workorderid = woq.workorderid
																							AND woq.queueid in {Startup.group}
																			   LEFT JOIN workorderhistory woh ON wo.workorderid = woh.workorderid 
																							  AND woh.historyid IN (SELECT diff.historyid
																												   FROM workorderhistorydiff diff
																												   WHERE diff.columnname = 'QUEUEID'
																												   AND diff.current_value in {Startup.group}
																												   )
																			   JOIN sduser u ON wos.ownerid = u.userid
																			   JOIN categorydefinition cd ON wos.categoryid = cd.categoryid 
																							  AND cd.categoryid not in {Startup.category}
																			   GROUP BY wo.workorderid, u.userid, (u.lastname || ' ') || u.firstname, wo.createdtime) t
																		) t
																	   JOIN (SELECT DISTINCT from_unixtime(wo.createdtime / 1000)::date AS diffdate
																		   FROM workorder wo) days ON days.diffdate >= t.appointdate::date AND days.diffdate <= t.userdate::date
																	   LEFT JOIN holidaydefinition hd ON days.diffdate = hd.holidaydate
																	   WHERE to_char(t.userdate, 'YYYY.MM.DD') between '{start}' and '{end}'
																	) t
																) t
															) t
														) t
													   GROUP BY t.id, t.userid, t.fio, t.appointdate, t.userdate
													   ORDER BY t.id, sum(t.reactionworktime)
													),
													 stat as
													(select id, userid, FIO, AppointDate, UserDate, ReactionWorkTime,
														case when r <= countfree then '0'::interval else ReactionWorkTime end ReactionWorkTimeFree
													   from (select q.id, q.userid, q.FIO, q.AppointDate, q.UserDate, q.ReactionWorkTime,
															row_number() over (partition by q.userid order by q.ReactionWorkTime desc) r,
															round(count(q2.id)::float / 100 * 3) countfree
														   from query q
														   join query q2 on q.userid = q2.userid
														   group by q.id, q.userid, q.FIO, q.AppointDate, q.UserDate, q.ReactionWorkTime
														) t
													)
												select null::int requestid, 'Итого по отделу' FIO, null::timestamp AppointDate, null::timestamp UserDate, 
													   to_char(avg(ReactionWorkTime), 'DD HH24:mi:ss') ReactionWorkTime,
													   to_char(avg(ReactionWorkTimeFree), 'DD HH24:mi:ss') ReactionWorkTimeFree
												  from stat
												union all
												select null::int , FIO, null::timestamp AppointDate, null::timestamp UserDate,
													   to_char(avg(ReactionWorkTime), 'DD HH24:mi:ss'),
													   to_char(avg(ReactionWorkTimeFree), 'DD HH24:mi:ss')
												  from stat
												  group by userid, FIO
												union all
												select id requestid, FIO, AppointDate, UserDate,
													   to_char(ReactionWorkTime, 'DD HH24:mi:ss'),
													   to_char(ReactionWorkTimeFree, 'DD HH24:mi:ss')
												  from stat
                                        ").ToList();
			}
			catch (Exception ex)
			{
				Program.logger.Error("Ошибка запроса отчета по времени выполнения заявок\n" + ex.Message);
			}
			return fact;
		}
	}
}
/*
select u.lastname, 'request ' || wo.workorderid nodoc, to_char(from_unixtime(wo.completedtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD') AS completedtime
  from workorder wo
  join workorderstates wos on wo.workorderid = wos.workorderid
  join sduser u on wos.ownerid = u.userid
  where u.userid in (688, 14460, 1217, 21082, 21485, 23811, 24044, 21486, 21634, 689, 1285, 697)
  and wo.completedtime <> 0
union
select u.lastname, 'request ' || wo.workorderid, to_char(from_unixtime(wo.completedtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD') AS completedtime
  from arc_workorder wo
  join sduser u on wo.ownerid = u.userid
  where u.userid in (688, 14460, 1217, 21082, 21485, 23811, 24044, 21486, 21634, 689, 1285, 697)
  and wo.completedtime <> 0
union
select u.lastname, 'task ' || td.taskid, to_char(from_unixtime(td.actualendtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD') AS completedtime
  from taskdetails td
  join sduser u on td.ownerid = u.userid
  where u.userid in (688, 14460, 1217, 21082, 21485, 23811, 24044, 21486, 21634, 689, 1285, 697)
  and td.actualendtime  <> 0
union
select u.lastname, 'task ' || td.taskid, to_char(from_unixtime(td.actualendtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD') AS completedtime
  from arc_taskdetails td
  join sduser u on td.ownerid = u.userid
  where u.userid in (688, 14460, 1217, 21082, 21485, 23811, 24044, 21486, 21634, 689, 1285, 697)
  and td.actualendtime  <> 0
order by lastname, completedtime
*/