const BUSINESS_TIME_ZONE = 'Asia/Tbilisi';

export function formatTime(timeStr: string): string {
  const date = new Date(timeStr);
  const hour = date.getHours().toString().padStart(2, '0');
  const minute = date.getMinutes().toString().padStart(2, '0');
  return `${hour}:${minute}`;
}

export function formatBusinessTime(timeStr: string | Date): string {
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: BUSINESS_TIME_ZONE,
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).format(typeof timeStr === 'string' ? new Date(timeStr) : timeStr);
}

export function formatBusinessDate(dateStr: string | Date): string {
  return new Intl.DateTimeFormat('en-US', {
    timeZone: BUSINESS_TIME_ZONE,
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(typeof dateStr === 'string' ? new Date(dateStr) : dateStr);
}

export function formatDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString('en-US', {
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  });
}

export function revertFormatTime(timeStr: string, dateStr: string): string {
  const [time, period = ''] = timeStr.trim().split(' ');
  const [parsedHour, minute] = time.split(':').map(Number);
  let hour = parsedHour;

  // 24-hour input ('14:30') has no period; the 12-hour branches are kept for legacy values.
  // Only one letter here since '10 p. m.' will be split as ['10', 'p.', 'm.'] and not ['10', 'p. m.']
  if (period.toLowerCase() === 'p.' && hour < 12) {
    hour += 12; // Convert PM to 24-hour format
  } else if (period.toLowerCase() === 'a.' && hour === 12) {
    hour = 0; // Convert 12 AM to 0 hours
  }

  const date = new Date();
  date.setFullYear(new Date(dateStr).getFullYear());
  date.setMonth(new Date(dateStr).getMonth());
  date.setDate(new Date(dateStr).getDate());
  date.setHours(hour, minute, 0, 0);

  console.log('Reverted time:', date.toISOString(), date);
  return date.toISOString();
}

export function revertFormatBusinessTime(timeStr: string, dateStr: string): string {
  const [time, period = ''] = timeStr.trim().split(' ');
  const [parsedHour, minute] = time.split(':').map(Number);
  let hour = parsedHour;
  const lowerPeriod = period.toLowerCase();

  if (lowerPeriod.startsWith('p') && hour < 12) {
    hour += 12;
  } else if (lowerPeriod.startsWith('a') && hour === 12) {
    hour = 0;
  }

  const date = new Date(revertFormatDate(dateStr));
  const localTimeAsUtc = Date.UTC(
    date.getFullYear(),
    date.getMonth(),
    date.getDate(),
    hour,
    minute
  );
  const businessParts = getBusinessDateTimeParts(new Date(localTimeAsUtc));
  const businessTimeAsUtc = Date.UTC(
    businessParts.year,
    businessParts.month - 1,
    businessParts.day,
    businessParts.hour,
    businessParts.minute
  );

  return new Date(localTimeAsUtc - (businessTimeAsUtc - localTimeAsUtc)).toISOString();
}

export function revertFormatDate(dateStr: string): string {
  const date = new Date(dateStr);

  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  const hours = `${date.getHours()}`.padStart(2, '0');
  const minutes = `${date.getMinutes()}`.padStart(2, '0');

  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

export function generateDateOptions(): string[] {
  const now = new Date();
  const businessNow = getBusinessDateTimeParts(now);
  const today = new Date(Date.UTC(businessNow.year, businessNow.month - 1, businessNow.day));
  const dateOptions: string[] = [];
  if (businessNow.hour * 60 + businessNow.minute > 21 * 60 + 15) {
    today.setUTCDate(today.getUTCDate() + 1);
  }

  const formatter = new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });

  for (let i = 0; i < 14; i++) {
    const date = new Date(today);
    date.setUTCDate(today.getUTCDate() + i);
    dateOptions.push(formatter.format(date));
  }

  return dateOptions;
}

export function generateTimeOptions(date: string): string[] {
  const dateObj = new Date(revertFormatDate(date));
  const now = new Date();
  const businessNow = getBusinessDateTimeParts(now);
  const isToday =
    dateObj.getFullYear() === businessNow.year &&
    dateObj.getMonth() + 1 === businessNow.month &&
    dateObj.getDate() === businessNow.day;
  if (!isToday) {
    return generateTimeOptionsForDay('10:00', '22:00');
  }
  let hours = businessNow.hour;
  let minutes = businessNow.minute;
  const moreThan45Minutes = minutes >= 45;
  const moreThan15Minutes = minutes >= 15 && minutes < 45;
  const moreThan0Minutes = minutes >= 0 && minutes < 15;

  // Since reservations can only be made 15 minutes in advance,
  // we adjust the current time to the next half hour if it's less than 15 minutes
  if (moreThan45Minutes) {
    minutes = 30;
    hours += 1;
  } else if (moreThan15Minutes) {
    minutes = 0;
    hours += 1;
  } else if (moreThan0Minutes) {
    minutes = 30;
  }

  if (hours > 21 || (hours === 21 && minutes > 30)) {
    return [];
  }

  return generateTimeOptionsForDay(
    `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}`,
    '22:00'
  );
}

function generateTimeOptionsForDay(startTime: string, endTime: string): string[] {
  const timeOptions: string[] = [];
  const [startHour, startMinute] = startTime.split(':').map(Number);
  const [endHour, endMinute] = endTime.split(':').map(Number);
  const endMinutes = endHour * 60 + endMinute;

  for (let minutes = startHour * 60 + startMinute; minutes < endMinutes; minutes += 30) {
    const hour = Math.floor(minutes / 60)
      .toString()
      .padStart(2, '0');
    const minute = (minutes % 60).toString().padStart(2, '0');
    timeOptions.push(`${hour}:${minute}`);
  }

  return timeOptions;
}

export function getBusinessDateString(date: Date = new Date()): string {
  const businessDate = getBusinessDateTimeParts(date);
  return `${businessDate.year}-${businessDate.month.toString().padStart(2, '0')}-${businessDate.day
    .toString()
    .padStart(2, '0')}`;
}

function getBusinessDateTimeParts(date: Date): {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
} {
  const parts = new Map(
    new Intl.DateTimeFormat('en-GB', {
      timeZone: BUSINESS_TIME_ZONE,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    })
      .formatToParts(date)
      .map(({ type, value }) => [type, value])
  );

  return {
    year: Number(parts.get('year')),
    month: Number(parts.get('month')),
    day: Number(parts.get('day')),
    hour: Number(parts.get('hour')),
    minute: Number(parts.get('minute')),
  };
}

export function combineDateAndTime(dateStr: string, timeStr: string): string {
  const date = new Date(dateStr);
  const time = new Date(timeStr);

  const combinedDate = new Date(
    Date.UTC(
      date.getFullYear(),
      date.getMonth(),
      date.getDate(),
      time.getUTCHours(),
      time.getUTCMinutes()
    )
  );

  return combinedDate.toISOString();
}

const MILLISECONDS_PER_MINUTE = 60_000;

export function addMinutesIso(isoStr: string, minutes: number): string {
  return new Date(Date.parse(isoStr) + minutes * MILLISECONDS_PER_MINUTE).toISOString();
}

export function minutesBetween(startIso: string, endIso: string): number {
  return Math.round((Date.parse(endIso) - Date.parse(startIso)) / MILLISECONDS_PER_MINUTE);
}

export function findClosestDuration(minutes: number, durations: readonly number[]): number {
  return durations.reduce((closest, duration) =>
    Math.abs(duration - minutes) < Math.abs(closest - minutes) ? duration : closest
  );
}

export function parseToISO(dateStr: string, timeStr: string): string {
  const [hoursMinutes, period = ''] = timeStr.trim().split(' ');
  const [hoursStr, minutesStr] = hoursMinutes.split(':');

  let hours = Number(hoursStr);
  const minutes = Number(minutesStr);

  const lowerPeriod = period.toLowerCase();
  if (lowerPeriod.includes('p') && hours !== 12) hours += 12;
  if (lowerPeriod.includes('a') && hours === 12) hours = 0;

  const date = new Date(dateStr);
  date.setHours(hours, minutes, 0, 0);
  return date.toISOString();
}

export function addAndRoundBookingTime(
  timeStr: string,
  bookingMinutes = 15,
  roundStep = 15
): string {
  const date = new Date(timeStr);
  const totalMinutes = date.getMinutes() + bookingMinutes;
  date.setMinutes(totalMinutes);

  const roundedMinutes = Math.ceil(date.getMinutes() / roundStep) * roundStep;
  date.setMinutes(roundedMinutes);

  return date.toISOString();
}
