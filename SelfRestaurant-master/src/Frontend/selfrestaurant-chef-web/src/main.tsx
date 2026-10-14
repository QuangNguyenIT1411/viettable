import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { LivePauseProvider } from "./lib/livePause";
import "../../shared/design-tokens.css";
import "../../shared/staff-ui.css";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter basename="/app/chef">
      <LivePauseProvider>
        <App />
      </LivePauseProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
