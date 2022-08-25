resource "google_service_account" "default" {
  account_id   = "${random_pet.prefix.id}-id"
  project      = "octopus-api-tester"
  display_name = random_pet.prefix.id
}

resource "local_file" "private_key" {
  content  = tls_private_key.default.private_key_pem
  filename = "private_key"
}

resource "google_compute_instance" "default" {
  name                      = "${random_pet.prefix.id}-instance"
  project                   = "octopus-api-tester"
  machine_type              = "e2-medium"
  allow_stopping_for_update = true

  boot_disk {
    initialize_params {
      image = "debian-cloud/debian-10"
    }
  }

  network_interface {
    network = "default"

    access_config {
      // Ephemeral IP
    }
  }

  metadata = {
    ssh-keys = "octopus:${tls_private_key.default.public_key_openssh}"
  }

  service_account {
    # Google recommends custom service accounts that have cloud-platform scope and permissions granted via IAM Roles.
    email  = google_service_account.default.email
    scopes = ["cloud-platform"]
  }

  provisioner "file" {
    source      = data.archive_file.data.output_path
    destination = "/tmp/data.zip"
  }

  connection {
    type        = "ssh"
    user        = "octopus"
    private_key = file(local_file.private_key.filename)
    host        = self.network_interface.0.access_config.0.nat_ip
  }

  provisioner "remote-exec" {
    script = "test.sh"
  }
}