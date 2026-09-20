"use client";

import { useEffect, useState } from "react";
import { listParcelsSummary } from "../services/landIntelligenceApi";

export function DashboardStats() {
  const [parcelTotal, setParcelTotal] = useState<string>("Unavailable");

  useEffect(() => {
    let cancelled = false;
    listParcelsSummary()
      .then((result) => {
        if (!cancelled) {
          setParcelTotal(String(result.totalCount));
        }
      })
      .catch(() => {
        if (!cancelled) {
          setParcelTotal("Unavailable");
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="cardGrid">
      <div className="statCard">
        <p className="statLabel">Pilot district</p>
        <p className="statValue">Hambantota</p>
        <span className="chip">Southern Province</span>
      </div>
      <div className="statCard">
        <p className="statLabel">Indexed parcels (API)</p>
        <p className="statValue">{parcelTotal}</p>
        <p className="statHint">Live count from GET /api/v1/land/parcels</p>
      </div>
      <div className="statCard">
        <p className="statLabel">Enrichment</p>
        <p className="statValue">Partial</p>
        <p className="statHint">Some GIS layers may be unavailable</p>
      </div>
      <div className="statCard" style={{ gridColumn: "span 2" }}>
        <p className="statLabel">Hambantota spatial overview</p>
        <p className="statHint">
          Use Find Suitable Land to run explainable recommendations against
          current parcel and GIS-derived evidence.
        </p>
      </div>
      <div className="statCard">
        <p className="statLabel">Available intelligence</p>
        <ul className="statHint" style={{ paddingLeft: "1rem", margin: 0 }}>
          <li>Administrative verification</li>
          <li>Road accessibility</li>
          <li>Water proximity (evidence)</li>
          <li>GIS soil group (evidence)</li>
          <li>Conservation intersections</li>
          <li>Erosion observations may be unavailable</li>
        </ul>
      </div>
    </div>
  );
}
