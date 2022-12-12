import sys, requests, shutil, os

# handle arguments
input = sys.argv
if (len(input)==1):
    sys.exit("No build number was supplied as the first argument")
latestBuildNum = input[1]
expiryTime = "1h" # 1 hour default expiry time
if (len(input)==3):
    expiryTime = input[2]


# Tell the world what we are about to do
print("Preparing to zip the latest build which is build number " + latestBuildNum)

# Turn folder into .zip for easy upload
buildFilePath = "C:\Build\{}\output".format(latestBuildNum)
output_filename = "Build{}".format(latestBuildNum)
shutil.make_archive(output_filename, 'zip', buildFilePath)
output_filename_complete = output_filename + '.zip'
print("Successfully created file " + output_filename + ". Preparing to upload...")

# Upload online
url = 'https://file.io/?expires={}'.format(expiryTime)
data = {
    "file": open(output_filename_complete, "rb")
}
response = requests.post(url, files=data)
res = response.json()
print("Successfully uploaded build to file.io. Resulting link found below:")
print(res["link"])

# remove the created zip file
data["file"].close()
os.remove(output_filename_complete)

