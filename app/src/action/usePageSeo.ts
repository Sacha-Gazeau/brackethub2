import { useEffect } from "react";

type PageSeoParams = {
  title: string;
  description: string;
};

const DEFAULT_TITLE = "BracketHub";
const DEFAULT_DESCRIPTION =
  "BracketHub is het platform voor esports toernooien, brackets en betting.";

function ensureMetaDescription() {
  let meta = document.querySelector('meta[name="description"]');

  if (!meta) {
    meta = document.createElement("meta");
    meta.setAttribute("name", "description");
    document.head.appendChild(meta);
  }

  return meta;
}

export function usePageSeo({ title, description }: PageSeoParams) {
  useEffect(() => {
    const previousTitle = document.title;
    const metaDescription = ensureMetaDescription();
    const previousDescription =
      metaDescription.getAttribute("content") ?? DEFAULT_DESCRIPTION;

    document.title = title;
    metaDescription.setAttribute("content", description);

    return () => {
      document.title = previousTitle || DEFAULT_TITLE;
      metaDescription.setAttribute("content", previousDescription);
    };
  }, [description, title]);
}
