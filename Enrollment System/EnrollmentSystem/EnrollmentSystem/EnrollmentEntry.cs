using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnrollmentSystem
{
    public partial class EnrollmentEntry : Form
    {
        string connectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\Users\mis\Desktop\Enrollment System\Jacaba1.accdb";
        public EnrollmentEntry()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IDNumberTextBox.Text))
            {
                MessageBox.Show("ID NUMBER IS EMPTY");
                return;
            }

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                connection.Open();

                if (IsStudentAlreadyEnrolled(connection, IDNumberTextBox.Text.Trim().ToUpper()))
                {
                    MessageBox.Show("STUDENT ALREADY ENROLLED");
                    return;
                }

                if (!AreClassesAvailable(connection))
                {
                    MessageBox.Show("ENROLLMENT DECLINED, PLEASE CHECK CORRECT DETAILS");
                    return;
                }

                EnrollStudent(connection);
                MessageBox.Show("ENROLLED");
                ClearForm();
            }
        }

        private bool IsStudentAlreadyEnrolled(OleDbConnection connection, string studentId)
        {
            using (OleDbCommand command = new OleDbCommand("SELECT 1 FROM ENROLLMENTHEADERFILE WHERE ENRHFSTUDID = @StudentId", connection))
            {
                command.Parameters.AddWithValue("@StudentId", studentId);
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    return reader.HasRows;
                }
            }
        }

        private bool AreClassesAvailable(OleDbConnection connection)
        {
            foreach (DataGridViewRow row in DataGridView.Rows)
            {
                if (row.IsNewRow) continue;

                string edpCode = row.Cells[0].Value.ToString();

                using (OleDbCommand command = new OleDbCommand("SELECT SSFMAXSIZE, SSFCLASSSIZE FROM SUBJECTSCHEDFILE WHERE SSFEDPCODE = @edpCode", connection))
                {
                    command.Parameters.AddWithValue("@edpCode", edpCode);
                    using (OleDbDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int maxSize = Convert.ToInt32(reader["SSFMAXSIZE"]);
                            int classSize = Convert.ToInt32(reader["SSFCLASSSIZE"]);

                            if (classSize >= maxSize)
                            {
                                MessageBox.Show($"CLASS {edpCode} IS FULL");
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private void EnrollStudent(OleDbConnection connection)
        {
            using (OleDbTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    InsertEnrollmentHeader(connection, transaction);
                    InsertEnrollmentDetails(connection, transaction);
                    UpdateSubjectSchedule(connection, transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void InsertEnrollmentHeader(OleDbConnection connection, OleDbTransaction transaction)
        {
            using (OleDbCommand command = new OleDbCommand(
                "INSERT INTO ENROLLMENTHEADERFILE (ENRHFSTUDID, ENRHFSTUDDATEENROLL, ENRHFSTUDSCHLYR, ENRHFSTUDENCODER, ENRHFSTUDTOTALUNITS, ENRHFSTUDSTATUS) " +
                "VALUES (@StudentId, @EnrollDate, @SchoolYear, @Encoder, @TotalUnits, @Status)", connection, transaction))
            {
                command.Parameters.AddWithValue("@StudentId", IDNumberTextBox.Text);
                command.Parameters.AddWithValue("@EnrollDate", DatePicker.Text.Trim());
                command.Parameters.AddWithValue("@SchoolYear", "2023-2024");
                command.Parameters.AddWithValue("@Encoder", NameLabel2.Text);
                command.Parameters.AddWithValue("@TotalUnits", Convert.ToInt16(TotalUnitsLabel.Text));
                command.Parameters.AddWithValue("@Status", "EN");

                command.ExecuteNonQuery();
            }
        }

        private void InsertEnrollmentDetails(OleDbConnection connection, OleDbTransaction transaction)
        {
            foreach (DataGridViewRow row in DataGridView.Rows)
            {
                if (row.IsNewRow) continue;

                using (OleDbCommand command = new OleDbCommand(
                    "INSERT INTO ENROLLMENTDETAILFILE (ENRDFSTUDID, ENRDFSTUDSUBJCODE, ENRDFSTUDEDPCODE) " +
                    "VALUES (@StudentId, @SubjectCode, @EdpCode)", connection, transaction))
                {
                    command.Parameters.AddWithValue("@StudentId", IDNumberTextBox.Text);
                    command.Parameters.AddWithValue("@SubjectCode", row.Cells[1].Value);
                    command.Parameters.AddWithValue("@EdpCode", row.Cells[0].Value);

                    command.ExecuteNonQuery();
                }
            }
        }

        private void UpdateSubjectSchedule(OleDbConnection connection, OleDbTransaction transaction)
        {
            foreach (DataGridViewRow row in DataGridView.Rows)
            {
                if (row.IsNewRow) continue;

                string edpCode = row.Cells[0].Value.ToString();

                using (OleDbCommand command = new OleDbCommand(
                    "UPDATE SUBJECTSCHEDFILE SET SSFCLASSSIZE = SSFCLASSSIZE + 1 WHERE SSFEDPCODE = @EdpCode", connection, transaction))
                {
                    command.Parameters.AddWithValue("@EdpCode", edpCode);
                    command.ExecuteNonQuery();
                }

                using (OleDbCommand command = new OleDbCommand(
                    "UPDATE SUBJECTSCHEDFILE SET SSFSTATUS = 'IN' WHERE SSFEDPCODE = @EdpCode AND SSFCLASSSIZE >= SSFMAXSIZE", connection, transaction))
                {
                    command.Parameters.AddWithValue("@EdpCode", edpCode);
                    command.ExecuteNonQuery();
                }
            }
        }

        private void ClearForm()
        {
            EDPCodeTextBox.Text = string.Empty;
            NameLabel2.Text = string.Empty;
            CourseLabelTwo.Text = string.Empty;
            YearLabelTwo.Text = string.Empty;
            IDNumberTextBox.Text = string.Empty;
            DataGridView.Rows.Clear();
        }

        private void StudentInfoGroupBox_Paint(object sender, PaintEventArgs e)
        {
            StudentInfoGroupBox.BackColor = Color.FromArgb(150, StudentInfoGroupBox.BackColor);
        }

        private void IDNumberTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                OleDbConnection thisConnection = new OleDbConnection(connectionString);
                thisConnection.Open();
                OleDbCommand thisCommand = thisConnection.CreateCommand();
                thisCommand.CommandText = "SELECT * FROM STUDENTFILE";
                OleDbDataReader thisDataReader = thisCommand.ExecuteReader();

                bool found = false;

                while (thisDataReader.Read())
                {
                    if (thisDataReader["STFSTUDID"].ToString().Trim() == IDNumberTextBox.Text.Trim())
                    {
                        string name = null;
                        string course = null;
                        string year = null;
                        found = true;
                        name = thisDataReader["STFSTUDLNAME"].ToString() + ", " + thisDataReader["STFSTUDFNAME"].ToString() + " " + thisDataReader["STFSTUDMNAME"].ToString().Substring(0, 1);
                        NameLabel2.Text = name;
                        course = thisDataReader["STFSTUDCOURSE"].ToString();
                        CourseLabelTwo.Text = course;
                        year = thisDataReader["STFSTUDYEAR"].ToString();
                        YearLabelTwo.Text = year;
                        break;
                    }
                }

                if (!found)
                    MessageBox.Show("STUDENT NOT FOUND");
            }
        }


        private void CancelButton_Click_1(object sender, EventArgs e)
        {
            IDNumberTextBox.Clear();
            NameLabel2.Text = null;
            CourseLabelTwo.Text = null;
            YearLabelTwo.Text = null;
            EDPCodeTextBox.Clear();
            TotalUnitsLabel.Text = null;
            DataGridView.Rows.Clear();
        }

        private void EDPCodeTextBox_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {



                foreach (DataGridViewRow row in DataGridView.Rows)
                {
                    string edpCode = EDPCodeTextBox.Text.Trim();
                    if (row.IsNewRow) continue;
                    if (row.Cells[0].Value != null && row.Cells[0].Value.ToString().Trim() == edpCode)
                    {
                        MessageBox.Show("EDP code already exists in the schedule.");
                        return;
                    }
                }

                using (OleDbConnection isConnection = new OleDbConnection(connectionString))
                {
                    isConnection.Open();

                    OleDbCommand command = new OleDbCommand("SELECT * FROM SUBJECTSCHEDFILE WHERE SSFEDPCODE = @edpCode", isConnection);
                    command.Parameters.AddWithValue("@edpCode", EDPCodeTextBox.Text.Trim());
                    OleDbDataReader reader = command.ExecuteReader();

                    if (!reader.Read())
                    {
                        MessageBox.Show("SCHEDULE NOT FOUND");
                        reader.Close();
                        return;
                    }

                }

                string days = "";
                string start = "";
                string end = "";
                bool conflict = false;
                bool closed = false;

                using (OleDbConnection thisConnection = new OleDbConnection(connectionString))
                {
                    thisConnection.Open();
                    string sql = "SELECT * FROM SUBJECTSCHEDFILE WHERE SSFEDPCODE = @EDPCode";
                    using (OleDbCommand thisCommand = new OleDbCommand(sql, thisConnection))
                    {
                        thisCommand.Parameters.AddWithValue("@EDPCode", EDPCodeTextBox.Text.Trim().ToUpper());
                        using (OleDbDataReader thisDataReader = thisCommand.ExecuteReader())
                        {
                            if (thisDataReader.Read())
                            {
                                days = thisDataReader["SSFDAYS"].ToString().ToUpper();
                                start = thisDataReader["SSFSTARTTIME"].ToString();
                                end = thisDataReader["SSFENDTIME"].ToString();
                            }
                        }
                    }

                    for (int i = 0; i < DataGridView.Rows.Count; i++)
                    {
                        if (DataGridView.Rows[i].Cells[0].Value == null) continue;

                        string daysInDataGrid = DataGridView.Rows[i].Cells[4].Value.ToString().ToUpper();
                        if (days == daysInDataGrid ||
                            (days == "MW" && (daysInDataGrid == "MON" || daysInDataGrid == "WED")) ||
                            (daysInDataGrid == "MW" && (days == "MON" || days == "WED")) ||
                            (days == "MWF" && (daysInDataGrid == "MON" || daysInDataGrid == "WED" || daysInDataGrid == "FRI")) ||
                            (daysInDataGrid == "MWF" && (days == "MON" || days == "WED" || days == "FRI")) ||
                            (days == "TTH" && (daysInDataGrid == "TUE" || daysInDataGrid == "THU")) ||
                            (daysInDataGrid == "TTH" && (days == "TUE" || days == "THU")) ||
                            (days == "TTHS" && (daysInDataGrid == "TUE" || daysInDataGrid == "THU" || daysInDataGrid == "SAT")) ||
                            (daysInDataGrid == "TTHS" && (days == "TUE" || days == "THU" || days == "SAT")) ||
                            (days == "FS" && (daysInDataGrid == "FRI" || daysInDataGrid == "SAT")) ||
                            (daysInDataGrid == "FS" && (days == "FRI" || days == "SAT")))
                        {
                            conflict = true;
                            break;
                        }
                    }

                    if (conflict)
                    {
                        TimeSpan startTime1 = DateTime.Parse(start).TimeOfDay;
                        TimeSpan endTime1 = DateTime.Parse(end).TimeOfDay;

                        for (int i = 0; i < DataGridView.Rows.Count; i++)
                        {
                            if (DataGridView.Rows[i].Cells[0].Value == null) continue;

                            TimeSpan startTime2 = DateTime.Parse(DataGridView.Rows[i].Cells[2].Value.ToString()).TimeOfDay;
                            TimeSpan endTime2 = DateTime.Parse(DataGridView.Rows[i].Cells[3].Value.ToString()).TimeOfDay;

                            if ((startTime1 < endTime2 && endTime1 > startTime2) ||
                                (startTime1 == startTime2 && endTime1 == endTime2))
                            {
                                conflict = true;
                                break;
                            }
                            else
                            {
                                conflict = false;
                            }
                        }
                    }

                    sql = "SELECT SSFSTATUS FROM SUBJECTSCHEDFILE WHERE SSFEDPCODE = @EDPCode";
                    using (OleDbCommand thisCommand = new OleDbCommand(sql, thisConnection))
                    {
                        thisCommand.Parameters.AddWithValue("@EDPCode", EDPCodeTextBox.Text.Trim().ToUpper());
                        using (OleDbDataReader thisDataReader = thisCommand.ExecuteReader())
                        {
                            if (thisDataReader.Read())
                            {
                                closed = thisDataReader["SSFSTATUS"].ToString().Trim().ToUpper() == "IN";
                            }
                        }
                    }

                    if (!conflict && !closed)
                    {
                        sql = "SELECT * FROM SUBJECTSCHEDFILE WHERE SSFEDPCODE = @EDPCode";
                        using (OleDbCommand thisCommand = new OleDbCommand(sql, thisConnection))
                        {
                            thisCommand.Parameters.AddWithValue("@EDPCode", EDPCodeTextBox.Text.Trim().ToUpper());
                            using (OleDbDataReader thisDataReader = thisCommand.ExecuteReader())
                            {
                                if (thisDataReader.Read())
                                {
                                    DateTime startTime = DateTime.Parse(thisDataReader["SSFSTARTTIME"].ToString());
                                    DateTime endTime = DateTime.Parse(thisDataReader["SSFENDTIME"].ToString());

                                    DataGridView.Rows.Insert(0, new object[] {
                            EDPCodeTextBox.Text.Trim().ToUpper(),
                            thisDataReader["SSFSUBJCODE"].ToString(),
                            startTime.ToShortTimeString(),
                            endTime.ToShortTimeString(),
                            thisDataReader["SSFDAYS"].ToString(),
                            thisDataReader["SSFROOM"].ToString()
                        });
                                }
                            }
                        }

                        sql = "SELECT SFSUBJUNITS FROM SUBJECTFILE WHERE SFSUBJCODE = @SubjectCode";
                        using (OleDbCommand thisCommand = new OleDbCommand(sql, thisConnection))
                        {
                            thisCommand.Parameters.AddWithValue("@SubjectCode", DataGridView.Rows[0].Cells[1].Value.ToString());
                            using (OleDbDataReader thisDataReader = thisCommand.ExecuteReader())
                            {
                                if (thisDataReader.Read())
                                {
                                    DataGridView.Rows[0].Cells[6].Value = thisDataReader["SFSUBJUNITS"].ToString();
                                }
                            }
                        }

                        int totalUnits = 0;
                        for (int i = 0; i < DataGridView.Rows.Count; i++)
                        {
                            if (DataGridView.Rows[i].Cells[6].Value != null)
                            {
                                totalUnits += Convert.ToInt32(DataGridView.Rows[i].Cells[6].Value);
                            }
                        }
                        TotalUnitsLabel.Text = "Units: " + totalUnits;
                    }
                    else if (conflict)
                    {
                        MessageBox.Show("Schedule is Conflict");
                    }
                    else if (closed)
                    {
                        MessageBox.Show("Schedule is Closed");
                    }
                }

            }
        }

        private void MainMenu_Click(object sender, EventArgs e)
        {
            MainEntry mainEntry = new MainEntry();
            mainEntry.Show();
            Hide();
        }
    }
}
