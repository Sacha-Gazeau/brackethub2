import { Link } from "react-router-dom";

type TournamentAdminNavProps = {
  slug: string;
  active: "dashboard" | "teams" | "stage";
};

const items = [
  { key: "dashboard", label: "Dashboard", suffix: "" },
  { key: "teams", label: "Teams", suffix: "/teams" },
  { key: "stage", label: "Structuur", suffix: "/stage" },
] as const;

export default function TournamentAdminNav({
  slug,
  active,
}: TournamentAdminNavProps) {
  return (
    <nav className="tournament-admin-nav" aria-label="Tournament admin navigation">
      {items.map((item) => (
        <Link
          key={item.key}
          className={`tournament-admin-nav__link ${
            active === item.key ? "tournament-admin-nav__link--active" : ""
          }`}
          to={`/tournament/${slug}/admin${item.suffix}`}
        >
          {item.label}
        </Link>
      ))}
    </nav>
  );
}
