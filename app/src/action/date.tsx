export const formatDate = (start: string, end: string) => {
  const startDate = new Date(start);
  const endDate = new Date(end);

  const startDay = startDate.getDate();
  const endDay = endDate.getDate();

  const month = startDate.toLocaleString("nl-NL", {
    month: "long",
  });

  return `${startDay}-${endDay} ${month}`;
};

export const formatDateYear = (start: string, end: string) => {
  const startDate = new Date(start);
  const endDate = new Date(end);

  const startDay = startDate.getDate();
  const endDay = endDate.getDate();

  const month = startDate.toLocaleString("nl-NL", {
    month: "long",
  });
  const year = startDate.getFullYear();

  return `${startDay}-${endDay} ${month} ${year}`;
};

export const formatHours = (start: string) => {
  const startDate = new Date(start);

  const hours = startDate.getHours();
  const minutes = startDate.getMinutes();

  const paddedMinutes = minutes.toString().padStart(2, "0");

  return `${hours}:${paddedMinutes}`;
};
export const dateYear = (start: string) => {
  const startDate = new Date(start);

  const startDay = startDate.getDate();

  const month = startDate.toLocaleString("nl-NL", {
    month: "long",
  });
  const year = startDate.getFullYear();

  return `${startDay} ${month} ${year}`;
};
