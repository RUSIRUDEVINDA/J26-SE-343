import type { LeaseInterestValuation } from "../types/landIntelligence";
import {
  buildValuationWorksheetSections,
  NOT_ENTERED,
  valuationFormTypeLabel,
} from "../utils/authorityValuationWorksheet";
import styles from "./AuthorityLeaseInterestReviewView.module.css";

export function AuthorityValuationWorksheet({
  valuation,
}: {
  valuation: LeaseInterestValuation;
}) {
  const sections = buildValuationWorksheetSections(valuation);

  return (
    <div
      className={styles.worksheet}
      data-testid="authority-valuation-worksheet"
      data-form-type={valuation.formType}
    >
      <div className={styles.worksheetHeader}>
        <h4 className={styles.worksheetTitle}>
          Revision {valuation.revision} ·{" "}
          {valuationFormTypeLabel(valuation.formType)}
        </h4>
        <span className={styles.manualTag}>
          Officer-entered values (manual — not calculated)
        </span>
      </div>

      {sections.map((section) => (
        <section
          key={section.id}
          className={styles.worksheetSection}
          aria-labelledby={`worksheet-${section.id}-title`}
        >
          <h5 id={`worksheet-${section.id}-title`} className={styles.worksheetSectionTitle}>
            {section.title}
          </h5>

          {section.fields?.length ? (
            <dl className={styles.detailsGrid}>
              {section.fields.map((field) => (
                <div key={`${section.id}-${field.label}`}>
                  <dt>{field.label}</dt>
                  <dd
                    className={
                      field.value === NOT_ENTERED ? styles.notEntered : undefined
                    }
                  >
                    {field.value}
                  </dd>
                </div>
              ))}
            </dl>
          ) : null}

          {section.tables?.map((table) => (
            <div key={table.title} className={styles.periodTableWrap}>
              <h6 className={styles.periodTableTitle}>{table.title}</h6>
              <p className={styles.fieldHelp}>
                Period amounts are shown exactly as entered. No total is
                calculated.
              </p>
              <table className={styles.periodTable}>
                <thead>
                  <tr>
                    <th scope="col">Year / period</th>
                    <th scope="col">Amount (LKR)</th>
                  </tr>
                </thead>
                <tbody>
                  {table.rows.map((row, index) => (
                    <tr key={`${table.title}-${index}`}>
                      <td
                        className={
                          row.period === NOT_ENTERED
                            ? styles.notEntered
                            : undefined
                        }
                      >
                        {row.period}
                      </td>
                      <td
                        className={
                          row.amount === NOT_ENTERED
                            ? styles.notEntered
                            : undefined
                        }
                      >
                        {row.amount}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}

          {section.notes?.length ? (
            <dl className={styles.notesList}>
              {section.notes.map((note) => (
                <div key={note.label}>
                  <dt>{note.label}</dt>
                  <dd
                    className={
                      note.value === NOT_ENTERED ? styles.notEntered : undefined
                    }
                  >
                    {note.value}
                  </dd>
                </div>
              ))}
            </dl>
          ) : null}
        </section>
      ))}
    </div>
  );
}
