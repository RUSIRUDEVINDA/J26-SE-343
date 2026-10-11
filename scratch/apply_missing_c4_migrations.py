import psycopg2

conn = psycopg2.connect("host=localhost port=5432 dbname=stateland_governance user=postgres password=1234")
conn.autocommit = False
cur = conn.cursor()

try:
    # 1. 20260918072800_GovernanceIntelligence_AddEarlyGovernanceScreeningEvaluations
    print("Applying 20260918072800_GovernanceIntelligence_AddEarlyGovernanceScreeningEvaluations...")
    cur.execute("""
        CREATE TABLE IF NOT EXISTS governance_intelligence.early_governance_screening_evaluations (
            assessment_id uuid NOT NULL,
            case_id character varying(100) NOT NULL,
            input_version character varying(50) NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            snapshot_schema_version integer NOT NULL,
            result_snapshot_json jsonb NOT NULL,
            CONSTRAINT "PK_early_governance_screening_evaluations" PRIMARY KEY (assessment_id)
        );
        INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
        VALUES ('20260918072800_GovernanceIntelligence_AddEarlyGovernanceScreeningEvaluations', '8.0.2')
        ON CONFLICT DO NOTHING;
    """)

    # 2. 20261008062258_GovernanceIntelligence_AddComplaintClassificationAssessments
    print("Applying 20261008062258_GovernanceIntelligence_AddComplaintClassificationAssessments...")
    cur.execute("""
        CREATE TABLE IF NOT EXISTS governance_intelligence.complaint_classification_assessments (
            assessment_id uuid NOT NULL,
            case_id character varying(100) NOT NULL,
            complaint_text text NOT NULL,
            model_version character varying(50) NOT NULL,
            predicted_category character varying(100) NOT NULL,
            administrative_procedural_integrity_probability numeric(18,17) NOT NULL,
            lease_revenue_payment_enforcement_probability numeric(18,17) NOT NULL,
            unauthorized_allocation_transfer_use_probability numeric(18,17) NOT NULL,
            protected_environmental_lease_misuse_probability numeric(18,17) NOT NULL,
            advisory_note text NOT NULL,
            closed_set_note text NOT NULL,
            assessed_at_utc timestamp with time zone NOT NULL,
            CONSTRAINT "PK_complaint_classification_assessments" PRIMARY KEY (assessment_id),
            CONSTRAINT "CK_complaint_classification_assessments_probability_range" CHECK (
                administrative_procedural_integrity_probability >= 0 AND administrative_procedural_integrity_probability <= 1 AND
                lease_revenue_payment_enforcement_probability >= 0 AND lease_revenue_payment_enforcement_probability <= 1 AND
                unauthorized_allocation_transfer_use_probability >= 0 AND unauthorized_allocation_transfer_use_probability <= 1 AND
                protected_environmental_lease_misuse_probability >= 0 AND protected_environmental_lease_misuse_probability <= 1
            ),
            CONSTRAINT "CK_complaint_classification_assessments_probability_sum" CHECK (
                abs(administrative_procedural_integrity_probability + lease_revenue_payment_enforcement_probability + unauthorized_allocation_transfer_use_probability + protected_environmental_lease_misuse_probability - 1.0) <= 0.000001
            )
        );
        INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
        VALUES ('20261008062258_GovernanceIntelligence_AddComplaintClassificationAssessments', '8.0.2')
        ON CONFLICT DO NOTHING;
    """)

    # 3. 20261008074415_GovernanceIntelligence_AddEarlyGovernanceCommissionerReferrals
    print("Applying 20261008074415_GovernanceIntelligence_AddEarlyGovernanceCommissionerReferrals...")
    cur.execute("""
        ALTER TABLE governance_intelligence.early_governance_screening_evaluations
            ADD COLUMN IF NOT EXISTS workflow_run_id uuid NULL;

        CREATE TABLE IF NOT EXISTS governance_intelligence.early_governance_referrals (
            referral_id uuid NOT NULL,
            correlation_id uuid NOT NULL,
            assessment_id uuid NOT NULL,
            case_id character varying(100) NOT NULL,
            workflow_run_id uuid NOT NULL,
            reason_code character varying(100) NOT NULL,
            reason character varying(2000) NOT NULL,
            evidence_references_json jsonb NOT NULL,
            requested_at_utc timestamp with time zone NOT NULL,
            delivery_state character varying(30) NOT NULL,
            delivery_attempt_count integer NOT NULL,
            last_attempt_at_utc timestamp with time zone NULL,
            acknowledged_at_utc timestamp with time zone NULL,
            commissioner_review_process_reference character varying(200) NULL,
            last_failure_code character varying(100) NULL,
            CONSTRAINT "PK_early_governance_referrals" PRIMARY KEY (referral_id),
            CONSTRAINT "CK_early_governance_referrals_delivery_attempt_count" CHECK (delivery_attempt_count >= 0),
            CONSTRAINT "CK_early_governance_referrals_delivery_state" CHECK (delivery_state IN ('Pending', 'Delivering', 'DeliveryFailed', 'Acknowledged')),
            CONSTRAINT "FK_early_governance_referrals_early_governance_screening_evalu~" FOREIGN KEY (assessment_id)
                REFERENCES governance_intelligence.early_governance_screening_evaluations (assessment_id) ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS "IX_early_governance_screening_evaluations_case_id_created_at_u~"
            ON governance_intelligence.early_governance_screening_evaluations (case_id, created_at_utc);

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_early_governance_referrals_assessment_id"
            ON governance_intelligence.early_governance_referrals (assessment_id);

        CREATE INDEX IF NOT EXISTS "IX_early_governance_referrals_case_id_requested_at_utc"
            ON governance_intelligence.early_governance_referrals (case_id, requested_at_utc);

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_early_governance_referrals_correlation_id"
            ON governance_intelligence.early_governance_referrals (correlation_id);

        CREATE INDEX IF NOT EXISTS "IX_early_governance_referrals_delivery_state"
            ON governance_intelligence.early_governance_referrals (delivery_state);

        INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
        VALUES ('20261008074415_GovernanceIntelligence_AddEarlyGovernanceCommissionerReferrals', '8.0.2')
        ON CONFLICT DO NOTHING;
    """)

    # 4. 20261009082924_GovernanceIntelligence_AddAuditAnchorReceiptAndOutbox
    print("Applying 20261009082924_GovernanceIntelligence_AddAuditAnchorReceiptAndOutbox...")
    cur.execute("""
        CREATE TABLE IF NOT EXISTS governance_intelligence.governance_audit_anchor_outbox (
            "Id" uuid NOT NULL,
            "AuditRecordId" uuid NOT NULL,
            "RecordHash" character varying(64) NOT NULL,
            "EngineType" integer NOT NULL,
            "RecordVersion" character varying(16) NOT NULL,
            "Status" integer NOT NULL,
            "RetryCount" integer NOT NULL,
            "NextAttemptAtUtc" timestamp with time zone NOT NULL,
            "LastErrorMessage" character varying(1000) NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "ProcessedAtUtc" timestamp with time zone NULL,
            CONSTRAINT "PK_governance_audit_anchor_outbox" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS governance_intelligence.governance_audit_anchor_receipts (
            "Id" uuid NOT NULL,
            "AuditRecordId" uuid NOT NULL,
            "RecordHash" character varying(64) NOT NULL,
            "AnchorStatus" integer NOT NULL,
            "TransactionReference" character varying(128) NULL,
            "BlockNumber" bigint NULL,
            "ContractAddress" character varying(64) NULL,
            "AnchoredAtUtc" timestamp with time zone NULL,
            "LastVerifiedAtUtc" timestamp with time zone NULL,
            "VerificationStatus" integer NOT NULL,
            "FailureReason" character varying(500) NULL,
            "RetryCount" integer NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_governance_audit_anchor_receipts" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "idx_gov_anchor_outbox_audit_record_id"
            ON governance_intelligence.governance_audit_anchor_outbox ("AuditRecordId");

        CREATE INDEX IF NOT EXISTS "idx_gov_anchor_outbox_status_next_attempt"
            ON governance_intelligence.governance_audit_anchor_outbox ("Status", "NextAttemptAtUtc");

        CREATE UNIQUE INDEX IF NOT EXISTS "idx_gov_anchor_receipt_audit_record_id"
            ON governance_intelligence.governance_audit_anchor_receipts ("AuditRecordId");

        CREATE INDEX IF NOT EXISTS "idx_gov_anchor_receipt_record_hash"
            ON governance_intelligence.governance_audit_anchor_receipts ("RecordHash");

        CREATE INDEX IF NOT EXISTS "idx_gov_anchor_receipt_status"
            ON governance_intelligence.governance_audit_anchor_receipts ("AnchorStatus");

        INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
        VALUES ('20261009082924_GovernanceIntelligence_AddAuditAnchorReceiptAndOutbox', '8.0.2')
        ON CONFLICT DO NOTHING;
    """)

    conn.commit()
    print("All 4 migrations applied successfully!")

except Exception as ex:
    conn.rollback()
    print("Migration failed:", ex)
    raise
finally:
    conn.close()

