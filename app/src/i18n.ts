import i18n from "i18next";
import { initReactI18next } from "react-i18next";

import nl from "./data/nl-BE.json";

i18n.use(initReactI18next).init({
  resources: {
    nl: {
      translation: nl,
    },
  },
  lng: "nl",
  fallbackLng: "nl",
  interpolation: {
    escapeValue: false,
  },
});

export default i18n;
