using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                reaction = db.Reaction.FromSqlRaw(@$"with query as 
                                            (select * 
                                               from it_motivation_second_line_with_calendar2
                                               where to_char(AppointDate, 'YYYY.MM.DD') between '{start}' and '{end}'
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
                                                    select u.lastname || ' ' || u.firstname AS fio, 'заявка ' || wo.workorderid nodoc, to_char(from_unixtime(wo.resolvedtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD') AS resolvedtime
                                                        from workorder wo
                                                        join workorderstates wos on wo.workorderid = wos.workorderid
                                                        join sduser u on wos.ownerid = u.userid
                                                        where u.userid in (688, 14460, 1217, 21082, 21485, 23811, 24044, 21486, 21634, 689, 1285, 697)
                                                        and wo.resolvedtime <> 0
                                                    union
                                                    select u.lastname || ' ' || u.firstname, 'задача ' || td.taskid, to_char(from_unixtime(td.actualendtime / 1000) + '03:00:00'::interval, 'YYYY.MM.DD')
                                                        from taskdetails td
                                                        join sduser u on td.ownerid = u.userid
                                                        where u.userid in (688, 14460, 1217, 21082, 21485, 23811, 24044, 21486, 21634, 689, 1285, 697)
                                                        and td.actualendtime  <> 0
                                                    ) t
                                                    where t.resolvedtime between '{start}' and '{end}'
                                                )
                                                select ' Итого по отделу' fio, null nodoc, null resolvedtime, count(*) storypoints
                                                    from stat
                                                union
                                                select fio, null, null, count(*)
                                                    from stat
                                                    group by fio
                                                union
                                                select fio, nodoc, resolvedtime, 1
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
    }
}