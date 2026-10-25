// Calendar-week pages: the parts that depend on today (pages are static; scripts/week-pages.py builds them).
(() => {
  const DAY = 864e5;
  const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
  const DAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const now = new Date();
  const today = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
  const iso = d => d.toISOString().slice(0, 10);
  const parse = s => new Date(s + "T00:00:00Z");
  const monday = d => new Date(d.getTime() - ((d.getUTCDay() + 6) % 7) * DAY);
  const thisMonday = monday(today);
  function isoWeek(d) {
    const t = new Date(d.getTime() + (4 - (d.getUTCDay() || 7)) * DAY);
    const year = t.getUTCFullYear();
    return { year, week: Math.ceil(((t - Date.UTC(year, 0, 1)) / DAY + 1) / 7) };
  }
  const long = d => `${DAYS[d.getUTCDay()]} ${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}`;

  document.querySelectorAll(`[data-date="${iso(today)}"]`).forEach(el => el.classList.add("is-today"));
  document.querySelectorAll(`tr[data-week-start="${iso(thisMonday)}"]`).forEach(el => el.classList.add("is-now"));

  // Week page: how far away this week is.
  const rel = document.querySelector(".rel[data-week-start]");
  if (rel) {
    const n = Math.round((parse(rel.dataset.weekStart) - thisMonday) / (7 * DAY));
    rel.textContent = n === 0 ? "This week" : n === 1 ? "Next week" : n === -1 ? "Last week"
      : n > 0 ? `In ${n} weeks` : `${-n} weeks ago`;
    rel.classList.toggle("is-now", n === 0);
  }

  // Hub: today's week.
  const hub = document.querySelector(".now[data-first]");
  if (hub) {
    const { year, week } = isoWeek(today);
    const sun = new Date(thisMonday.getTime() + 6 * DAY);
    document.getElementById("now-cw").textContent = week;
    document.getElementById("now-title").textContent = `It’s week ${week}`;
    document.getElementById("now-range").textContent =
      `Calendar week ${week} of ${year}: ${long(thisMonday)} – ${long(sun)} ${sun.getUTCFullYear()}.`;
    const link = document.getElementById("now-link");
    if (year >= +hub.dataset.first && year <= +hub.dataset.last) {
      link.href = `${year}/cw${week}/`;
      link.textContent = `See week ${week}`;
    }
    document.title = `What week is it? Week ${week}, ${year} | Weekmark`;
  }
})();
