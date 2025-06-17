                }
                byte[] buffer = new byte[4096];

                int bytesRead = stream!.Read(buffer, 0, buffer.Length);
                string initialMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                string[] messageParts = initialMessage.Split('|'); 