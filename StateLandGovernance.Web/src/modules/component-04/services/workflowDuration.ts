export type WorkflowTimeline={eventTimeZone?:string;events:ReadonlyArray<{timestamp:string}>};
export type WorkflowDurationAssessment={status:"warning-short"|"warning-long"|"acceptable";message:string};

type LocalDate={year:number;month:number;day:number};
const daysInMonth=(year:number,month:number)=>new Date(Date.UTC(year,month,0)).getUTCDate();
const compare=(left:LocalDate,right:LocalDate)=>left.year-right.year||left.month-right.month||left.day-right.day;
const plusTwoCalendarMonths=({year,month,day}:LocalDate):LocalDate=>{
 const total=year*12+month-1+2;const nextYear=Math.floor(total/12);const nextMonth=total%12+1;
 return {year:nextYear,month:nextMonth,day:Math.min(day,daysInMonth(nextYear,nextMonth))};
};
function localDate(timestamp:string,timeZone:string):LocalDate|null{
 const instant=new Date(timestamp);if(Number.isNaN(instant.getTime()))return null;
 const parts=new Intl.DateTimeFormat("en-CA",{timeZone,year:"numeric",month:"2-digit",day:"2-digit"}).formatToParts(instant);
 const value=(type:string)=>Number(parts.find(part=>part.type===type)?.value);
 const year=value("year"),month=value("month"),day=value("day");
 return Number.isInteger(year)&&Number.isInteger(month)&&Number.isInteger(day)?{year,month,day}:null;
}

export function assessWorkflowDuration(timeline:WorkflowTimeline|undefined,elapsedDays:number):WorkflowDurationAssessment|null{
 if(!Number.isFinite(elapsedDays)||!timeline||timeline.events.length<2)return null;
 if(elapsedDays<7)return {status:"warning-short",message:"Warning: the recorded workflow completed in less than one week."};
 const timeZone=timeline.eventTimeZone??"Asia/Colombo";
 const dates=timeline.events.map(event=>localDate(event.timestamp,timeZone));
 if(dates.some(date=>date===null))return null;
 const completeDates=dates as LocalDate[];
 const start=completeDates[0]!,end=completeDates[completeDates.length-1]!;
 if(compare(end,plusTwoCalendarMonths(start))>0)return {status:"warning-long",message:"Warning: the recorded workflow took more than two calendar months."};
 return {status:"acceptable",message:"The recorded workflow duration is acceptable."};
}
