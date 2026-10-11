import psycopg2

conn = psycopg2.connect("host=localhost port=5432 dbname=stateland_governance user=postgres password=1234")
cur = conn.cursor()
cur.execute('SELECT "MigrationId" FROM governance_intelligence.__ef_migrations_history ORDER BY 1;')
print("Applied migrations in governance_intelligence:")
for r in cur.fetchall():
    print(" ", r[0])

# Check tables in governance_intelligence
cur.execute("SELECT table_name FROM information_schema.tables WHERE table_schema = 'governance_intelligence';")
print("\nTables in governance_intelligence:")
for r in cur.fetchall():
    print(" ", r[0])

conn.close()
